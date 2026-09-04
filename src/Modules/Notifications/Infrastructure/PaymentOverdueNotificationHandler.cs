using System.Globalization;
using System.Text.Json;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>
/// M07-1 → M11: Vadesi geçen ödeme kaydı için bildirim üretir. Alıcılar:
/// <list type="bullet">
///   <item>ödemeyi takip eden <b>öğretmen</b> (koşulsuz — kendi tahsilat kaydı),</item>
///   <item>öğrencinin onaylı <b>Premium</b> velileri, yalnız "ödemeler" tercihi açıkken (PRD 9.3).</item>
/// </list>
/// Push burada gönderilmez — <c>NotificationDispatcher</c> turu <c>PushedOnUtc</c>'si boş kayıtları
/// toplu gönderir (Öğrenilenler #40/#55). Replay koruması ortak inbox üzerinden.
/// </summary>
public sealed class PaymentOverdueNotificationHandler : IdempotentIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IParentNotificationDirectory _directory;
    private readonly IUserNotificationRepository _userNotifications;
    private readonly IIdGenerator _idGenerator;

    public PaymentOverdueNotificationHandler(
        NotificationsDbContext dbContext,
        IParentNotificationDirectory directory,
        IUserNotificationRepository userNotifications,
        IIdGenerator idGenerator,
        IClock clock)
        : base(dbContext, clock)
    {
        _directory = directory;
        _userNotifications = userNotifications;
        _idGenerator = idGenerator;
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Payments"
            && integrationEvent.Name == "PaymentBecameOverdueDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PaymentBecameOverduePayload>(envelope.Payload, JsonOptions);
        if (payload is null)
        {
            return false;
        }

        var amount = payload.OutstandingAmount.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
        var dueDate = payload.DueDateUtc.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR"));

        await AddAsync(
            payload.TeacherUserId,
            "Ödeme vadesi geçti",
            $"{dueDate} vadeli {amount} {payload.Currency} tutarındaki ödeme henüz tahsil edilmedi.",
            "/payments",
            cancellationToken);

        var parents = await _directory.GetApprovedParentsForStudentAsync(payload.StudentId, cancellationToken);
        foreach (var parent in parents)
        {
            // Premium kapısı (PRD 9.3) + "ödemeler" tercih kapısı.
            if (parent.Tier != MembershipTier.Premium || !parent.Prefs.Payments)
            {
                continue;
            }

            await AddAsync(
                parent.ParentUserId,
                "Ödeme vadesi geçti",
                $"{dueDate} vadeli {amount} {payload.Currency} tutarındaki ödeme henüz tahsil edilmedi.",
                "/parent/payments",
                cancellationToken);
        }

        return true;
    }

    private Task AddAsync(Guid userId, string title, string body, string route, CancellationToken cancellationToken)
        => _userNotifications.AddAsync(
            new UserNotification(_idGenerator.New(), userId, UserNotificationKind.PaymentOverdue, title, body, route, Clock.UtcNow),
            cancellationToken);

    private sealed record PaymentBecameOverduePayload(
        Guid PaymentRecordId,
        Guid TeacherUserId,
        Guid StudentId,
        decimal OutstandingAmount,
        string Currency,
        DateTime DueDateUtc,
        DateTime MarkedOnUtc);
}
