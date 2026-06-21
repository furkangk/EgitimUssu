namespace EgitimUssu.Shared.Contracts;

public sealed record IntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    string Name,
    string SourceModule,
    string Payload) : IIntegrationEvent;
