using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

public interface IIntegrationEventHandler
{
    bool CanHandle(IIntegrationEvent integrationEvent);

    Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
