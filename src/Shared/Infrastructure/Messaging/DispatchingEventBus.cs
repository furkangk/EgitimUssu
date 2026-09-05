using System.Diagnostics;
using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

public sealed class DispatchingEventBus(
    IServiceScopeFactory scopeFactory,
    ILogger<DispatchingEventBus> logger) : IEventBus
{
    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Published integration event {EventName} from {Module}: {Payload}",
            integrationEvent.Name,
            integrationEvent.SourceModule,
            JsonSerializer.Serialize(integrationEvent));

        using var scope = scopeFactory.CreateScope();
        var matchingHandlers = scope.ServiceProvider
            .GetServices<IIntegrationEventHandler>()
            .Where(handler => handler.CanHandle(integrationEvent))
            .ToArray();

        foreach (var handler in matchingHandlers)
        {
            // C-04: Handler başına span. Aynı olayı birden çok modül dinlediğinde, "hangisi yavaş /
            // hangisi patladı" sorusunun cevabı ancak burada ayrışır — tek bir dispatch span'ının
            // altında hepsi tek bir süreye karışırdı.
            using var activity = EgitimUssuActivitySource.Instance.StartActivity(
                $"handler {handler.GetType().Name}",
                ActivityKind.Internal);

            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
    }
}
