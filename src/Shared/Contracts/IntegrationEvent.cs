using System.Text.Json;

namespace EgitimUssu.Shared.Contracts;

public sealed record IntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    string Name,
    string SourceModule,
    string Payload) : IIntegrationEvent;

/// <summary>
/// Outbox yazım ve okuma tarafının aynı sözleşme serileştirmesini paylaştığı tek kaynak.
/// Yazım (ModuleDbContext, JsonDomainEventMapper) ile okuma (OutboxProcessor) bu tek
/// <see cref="Options"/> örneğini kullanmalıdır; ayrışırsa (K3) event alanları null deserialize olur.
/// </summary>
public static class IntegrationEventSerialization
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
