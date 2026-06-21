namespace EgitimUssu.Shared.Infrastructure.Persistence;

public sealed record OutboxBatchItem(
    Type DbContextType,
    Guid MessageId,
    string Module,
    string Type,
    string Payload,
    DateTime OccurredOnUtc);
