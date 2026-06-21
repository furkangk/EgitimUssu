using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
