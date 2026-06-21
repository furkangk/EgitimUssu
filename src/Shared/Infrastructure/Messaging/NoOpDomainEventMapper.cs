using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

public sealed class NoOpDomainEventMapper : IDomainEventMapper
{
    public IReadOnlyCollection<IIntegrationEvent> Map(string sourceModule, DomainEvent domainEvent) => [];
}
