using System.Text.Json;
using EgitimUssu.Shared.Contracts;
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
            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
    }
}
