using EgitimUssu.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

/// <summary>
/// Integration event'i, onu işleyebilen tüm handler'lara kendi scope'unda dağıtır.
/// </summary>
public sealed class DispatchingEventBus(
    IServiceScopeFactory scopeFactory,
    ILogger<DispatchingEventBus> logger) : IEventBus
{
    /// <summary>Olayı eşleşen handler'lara sırayla iletir.</summary>
    /// <param name="integrationEvent">Yayınlanacak olay.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>İşlem görevi.</returns>
    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        // A-03: Payload BİLEREK loglanmıyor. Zarfın `Payload` alanı serileştirilmiş domain event'in
        // tamamıdır: `UserRegisteredDomainEvent` kullanıcının e-postasını, `MessageSentDomainEvent`
        // özel mesaj önizlemesini taşır. Bunlar Information seviyesinde basıldığında toplayıcıya
        // gidip uzun süre saklanıyordu — kalıcı bir KVKK sorunu. Korelasyon için EventId yeter;
        // içeriğe bakmak gerekirse kaynağı `outbox_messages` tablosudur.
        logger.LogDebug(
            "Published integration event {EventName} ({EventId}) from {Module}",
            integrationEvent.Name,
            integrationEvent.EventId,
            integrationEvent.SourceModule);

        using var scope = scopeFactory.CreateScope();
        var matchingHandlers = scope.ServiceProvider
            .GetServices<IIntegrationEventHandler>()
            .Where(handler => handler.CanHandle(integrationEvent))
            .ToArray();

        foreach (var handler in matchingHandlers)
        {
            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
    }
}
