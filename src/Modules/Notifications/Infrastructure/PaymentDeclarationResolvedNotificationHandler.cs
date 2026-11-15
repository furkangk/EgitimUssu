using System.Text.Json;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>Veliye kendi beyanının sonucunu bildirir; bildirim ve inbox aynı transaction'dadır.</summary>
public sealed class PaymentDeclarationResolvedNotificationHandler(NotificationsDbContext dbContext, IIdGenerator ids, IClock clock)
    : IdempotentIntegrationEventHandler(dbContext, clock)
{
    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Payments" && integrationEvent.Name == "ParentPaymentDeclarationResolvedDomainEvent";

    protected override Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<Resolution>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null || payload.ParentUserId == Guid.Empty || payload.Status is not (2 or 3))
            throw new PoisonMessageException("Ödeme beyanı olay yükü geçersiz.");

        var confirmed = payload.Status == 2;
        dbContext.UserNotifications.Add(new UserNotification(
            ids.New(), payload.ParentUserId, UserNotificationKind.System, "Ödeme beyanı sonuçlandı",
            confirmed ? "Ödeme beyanınız onaylandı." : "Ödeme beyanınız reddedildi. Güncel ödeme durumunu kontrol edebilirsiniz.",
            "/parent", Clock.UtcNow));
        return Task.FromResult(true);
    }

    // Mevcut Payments olay şeması tüketilir; modüller arasında domain bağımlılığı yoktur.
    private sealed record Resolution(Guid ParentUserId, int Status);
}
