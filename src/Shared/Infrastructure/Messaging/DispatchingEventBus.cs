using System.Diagnostics;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Observability;
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
        var handlers = scope.ServiceProvider
            .GetServices<IIntegrationEventHandler>()
            .ToArray();

        var failures = new List<Exception>();
        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Activity? activity = null;
            try
            {
                if (!handler.CanHandle(integrationEvent)) continue;
                activity = EgitimUssuActivitySource.Instance.StartActivity(
                    $"handler {handler.GetType().Name}", ActivityKind.Internal);
                await handler.HandleAsync(integrationEvent, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var failure = new IntegrationEventHandlerException(handler.GetType().FullName ?? handler.GetType().Name, exception);
                failures.Add(failure);
                activity?.SetStatus(ActivityStatusCode.Error, failure.Message);
                logger.LogWarning("Integration event {EventId}: {HandlerFailure}", integrationEvent.EventId, failure.Message);
            }
            finally
            {
                activity?.Dispose();
            }
        }

        if (failures.Count > 0)
            throw new AggregateException("Integration event handlers failed.", failures);
    }
}
