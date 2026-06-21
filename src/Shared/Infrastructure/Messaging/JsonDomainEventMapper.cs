using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

public sealed class JsonDomainEventMapper : IDomainEventMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyCollection<IIntegrationEvent> Map(string sourceModule, DomainEvent domainEvent)
    {
        return
        [
            new IntegrationEvent(
                domainEvent.EventId,
                domainEvent.OccurredOnUtc,
                domainEvent.GetType().Name,
                sourceModule,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions))
        ];
    }
}
