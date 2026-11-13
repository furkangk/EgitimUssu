using System.Text.Json;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>
/// M10-3 → M11: Konu hedefi otomatik kapandığında öğrenciye kutlama bildirimi.
/// Olay <c>StudentId</c> (öğrenci <b>profili</b>) taşır; alıcı kullanıcı kimliği
/// <see cref="IStudentDirectory"/> ile çözülür (Öğrenilenler #70 deseni).
/// Push burada gönderilmez — <c>NotificationDispatcher</c> turu üstlenir.
/// </summary>
public sealed class TopicGoalAchievedNotificationHandler : IdempotentIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationEventSerialization.Options;
    private readonly IStudentDirectory _students;
    private readonly IUserNotificationRepository _userNotifications;
    private readonly IIdGenerator _idGenerator;

    public TopicGoalAchievedNotificationHandler(
        NotificationsDbContext dbContext,
        IStudentDirectory students,
        IUserNotificationRepository userNotifications,
        IIdGenerator idGenerator,
        IClock clock)
        : base(dbContext, clock)
    {
        _students = students;
        _userNotifications = userNotifications;
        _idGenerator = idGenerator;
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent) =>
        integrationEvent.SourceModule == "ProgressTracking"
        && integrationEvent.Name == "TopicGoalAchievedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TopicGoalAchievedPayload>(envelope.Payload, JsonOptions);
        if (payload is null)
        {
            return false; // bozuk yük: inbox'a yazılmaz, yeniden denenebilir
        }

        var studentUserId = await _students.GetOwnerUserIdAsync(payload.StudentId, cancellationToken);
        if (studentUserId is null)
        {
            // Bağlı kullanıcı yok (öğrenci hesabını henüz devralmadı): bildirim atlanır ama olay
            // işlenmiş sayılır — aksi hâlde outbox bunu sonsuza dek yeniden denerdi.
            return true;
        }

        await _userNotifications.AddAsync(
            new UserNotification(
                _idGenerator.New(),
                studentUserId.Value,
                UserNotificationKind.System,
                "Hedefine ulaştın 🎯",
                $"{payload.Subject} dersinde \"{payload.Topic}\" konusu için koyduğun hedefi tamamladın.",
                "/student/progress",
                Clock.UtcNow),
            cancellationToken);

        return true;
    }

    private sealed record TopicGoalAchievedPayload(
        Guid TopicGoalId,
        Guid StudentId,
        string Subject,
        string Topic,
        DateTime AchievedOnUtc);
}
