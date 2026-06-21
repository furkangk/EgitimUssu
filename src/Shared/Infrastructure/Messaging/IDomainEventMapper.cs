using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

public interface IDomainEventMapper
{
    IReadOnlyCollection<IIntegrationEvent> Map(string sourceModule, DomainEvent domainEvent);
}
