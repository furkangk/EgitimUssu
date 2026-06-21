using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

public sealed class OutboxProcessor(
    IOutboxStore outboxStore,
    IEventBus eventBus,
    IOptions<OutboxOptions> options) : IOutboxProcessor
{
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var batch = await outboxStore.FetchPendingAsync(options.Value.BatchSize, cancellationToken);

        foreach (var item in batch)
        {
            var integrationEvent = JsonSerializer.Deserialize<IntegrationEvent>(item.Payload);
            if (integrationEvent is null)
            {
                continue;
            }

            await eventBus.PublishAsync(integrationEvent, cancellationToken);
        }

        await outboxStore.MarkProcessedAsync(batch, cancellationToken);
        return batch.Count;
    }
}
