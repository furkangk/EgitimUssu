namespace EgitimUssu.Shared.Infrastructure.Persistence;

public sealed record OutboxBatchItem(
    Type DbContextType,
    Guid MessageId,
    string Module,
    string Type,
    string Payload,
    DateTime OccurredOnUtc,
    // C-04: Mesajı üreten isteğin W3C traceparent'ı; yoksa null.
    string? TraceParent = null);
