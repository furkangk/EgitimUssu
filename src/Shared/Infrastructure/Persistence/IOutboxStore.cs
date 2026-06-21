namespace EgitimUssu.Shared.Infrastructure.Persistence;

public interface IOutboxStore
{
    Task<IReadOnlyCollection<OutboxBatchItem>> FetchPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(IReadOnlyCollection<OutboxBatchItem> items, CancellationToken cancellationToken = default);
}
