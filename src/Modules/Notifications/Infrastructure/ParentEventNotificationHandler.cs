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
/// </summary>
public sealed class ParentEventNotificationHandler : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IParentNotificationDirectory _directory;
    private readonly IParentNotificationRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public ParentEventNotificationHandler(
        IParentNotificationDirectory directory,
        IParentNotificationRepository repository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _directory = directory;
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent switch
        {
            { SourceModule: "Assignments", Name: "AssignmentCreatedDomainEvent" } => true,
            { SourceModule: "LessonSessions", Name: "LessonSessionCompletedDomainEvent" } => true,
            { SourceModule: "Payments", Name: "PaymentRecordUpdatedDomainEvent" } => true,
            { SourceModule: "Parents", Name: "ParentLinkConnectionNoticeDomainEvent" } => true,
            _ => false
        };

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent envelope)
        {
            return;
        }

        if (await _repository.HasProcessedAsync(envelope.EventId, cancellationToken))
        {
            return;
        }

        var mapped = Map(envelope);
        if (mapped is null)
        {
            return;
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
                new ParentNotification(_idGenerator.New(), target.ParentUserId, studentId, type, title, message, _clock.UtcNow),
                cancellationToken);
        }

        _repository.MarkProcessed(envelope.EventId, envelope.Name, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
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
