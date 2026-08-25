using System.Text.Json;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>
/// Olay bazlı veli bildirim üretici (Veli V-E). Yalnız **Premium** veliye ve ilgili tercih açıkken üretir.
/// Kaynaklar: Assignments/`AssignmentCreatedDomainEvent`, LessonSessions/`LessonSessionCompletedDomainEvent`,
/// Payments/`PaymentRecordUpdatedDomainEvent`, Parents/`ParentLinkConnectionNoticeDomainEvent`.
/// Event dedup artık ortak inbox üzerinden (<see cref="IdempotentIntegrationEventHandler"/>); haftalık-özet
/// dedup'ı (ayrı bir tablo/anahtar uzayı, <c>processed_integration_events</c>) bu handler'ı ETKİLEMEZ —
/// yalnız <see cref="ParentWeeklySummaryService"/> onu kullanmaya devam eder.
/// </summary>
public sealed class ParentEventNotificationHandler : IdempotentIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IParentNotificationDirectory _directory;
    private readonly IParentNotificationRepository _repository;
    private readonly IIdGenerator _idGenerator;

    public ParentEventNotificationHandler(
        NotificationsDbContext dbContext,
        IParentNotificationDirectory directory,
        IParentNotificationRepository repository,
        IIdGenerator idGenerator,
        IClock clock)
        : base(dbContext, clock)
    {
        _directory = directory;
        _repository = repository;
        _idGenerator = idGenerator;
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent switch
        {
            { SourceModule: "Assignments", Name: "AssignmentCreatedDomainEvent" } => true,
            { SourceModule: "LessonSessions", Name: "LessonSessionCompletedDomainEvent" } => true,
            { SourceModule: "Payments", Name: "PaymentRecordUpdatedDomainEvent" } => true,
            { SourceModule: "Parents", Name: "ParentLinkConnectionNoticeDomainEvent" } => true,
            _ => false
        };

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var mapped = Map(envelope);
        if (mapped is null)
        {
            // Bilinmeyen/bozuk payload: eski davranışla birebir — işlenmiş sayılmaz (inbox'a yazılmaz), yeniden denenebilir.
            return false;
        }

        var (studentId, type, requiredPref, title, message) = mapped.Value;

        var targets = await _directory.GetApprovedParentsForStudentAsync(studentId, cancellationToken);
        foreach (var target in targets)
        {
            // Premium kapısı (PRD 9.3): yalnız Premium veliye bildirim.
            if (target.Tier != MembershipTier.Premium)
            {
                continue;
            }

            // Tercih kapısı: ilgili anahtar kapalıysa atla (güvenlik bildiriminde tercih koşulsuz → requiredPref null).
            if (requiredPref is not null && !requiredPref(target.Prefs))
            {
                continue;
            }

            await _repository.AddAsync(
                new ParentNotification(_idGenerator.New(), target.ParentUserId, studentId, type, title, message, Clock.UtcNow),
                cancellationToken);
        }

        // Eski davranışla birebir: hedef bulunamasa/hepsi Premium-dışı veya tercih-kapalı olsa bile
        // olay "işlenmiş" sayılır (eskiden MarkProcessed koşulsuzdu) → inbox'a yazılır, tekrar denenmez.
        return true;
    }

    private static (Guid StudentId, ParentNotificationType Type, Func<ParentNotificationPrefs, bool>? RequiredPref, string Title, string Message)? Map(IntegrationEvent envelope)
    {
        switch (envelope.Name)
        {
            case "AssignmentCreatedDomainEvent":
            {
                var p = JsonSerializer.Deserialize<AssignmentCreatedPayload>(envelope.Payload, JsonOptions);
                return p is null ? null : (p.StudentId, ParentNotificationType.NewAssignment, prefs => prefs.MissedAssignment,
                    "Yeni ödev", "Çocuğunuza yeni bir ödev verildi.");
            }

            case "LessonSessionCompletedDomainEvent":
            {
                var p = JsonSerializer.Deserialize<LessonSessionCompletedPayload>(envelope.Payload, JsonOptions);
                return p is null ? null : (p.StudentId, ParentNotificationType.LessonCompleted, prefs => prefs.LessonReminders,
                    "Ders tamamlandı", "Çocuğunuzun bir dersi tamamlandı.");
            }

            case "PaymentRecordUpdatedDomainEvent":
            {
                var p = JsonSerializer.Deserialize<PaymentRecordUpdatedPayload>(envelope.Payload, JsonOptions);
                return p is null ? null : (p.StudentId, ParentNotificationType.PaymentUpdate, prefs => prefs.Payments,
                    "Ödeme güncellemesi", "Bir ödeme kaydı güncellendi.");
            }

            case "ParentLinkConnectionNoticeDomainEvent":
            {
                var p = JsonSerializer.Deserialize<ParentLinkConnectionNoticePayload>(envelope.Payload, JsonOptions);
                // Güvenlik bildirimi: tercih koşulsuz (RequiredPref = null), yalnız Premium kapısı uygulanır.
                return p is null ? null : (p.StudentId, ParentNotificationType.LinkConnected, (Func<ParentNotificationPrefs, bool>?)null,
                    "Yeni veli bağlantısı", "Çocuğunuza bir veli hesabı bağlandı.");
            }

            default:
                return null;
        }
    }

    private sealed record AssignmentCreatedPayload(Guid AssignmentId, Guid StudentId, Guid TeacherUserId, Guid? LessonSessionId, DateTime CreatedOnUtc);
    private sealed record LessonSessionCompletedPayload(Guid LessonSessionId, Guid? LessonScheduleId, Guid TeacherUserId, Guid StudentId, DateTime CompletedOnUtc);
    private sealed record PaymentRecordUpdatedPayload(Guid PaymentRecordId, Guid TeacherUserId, Guid StudentId, DateTime UpdatedOnUtc);
    private sealed record ParentLinkConnectionNoticePayload(Guid LinkId, Guid StudentId, Guid ConnectedParentUserId, Guid? ExistingPrimaryParentUserId, bool IsPrimaryContact, DateTime ConnectedOnUtc);
}
