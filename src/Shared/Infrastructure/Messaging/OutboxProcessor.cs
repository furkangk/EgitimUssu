using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Persistence;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

public sealed class OutboxProcessor(
    IOutboxStore outboxStore,
    IEventBus eventBus) : IOutboxProcessor
{
    public Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
        => outboxStore.ProcessPendingAsync(PublishAsync, cancellationToken);

    private async Task PublishAsync(OutboxBatchItem item, CancellationToken cancellationToken)
    {
        // K5: Deserialize başarısızlığı artık sessizce düşürülmez; hata olarak fırlatılır ki
        // mesaj retry/dead-letter akışına girsin (eski davranış mesajı "processed" işaretleyip kaybediyordu).
        var integrationEvent = JsonSerializer.Deserialize<IntegrationEvent>(item.Payload, IntegrationEventSerialization.Options)
            ?? throw new InvalidOperationException(
                $"Outbox mesajı {item.MessageId} ({item.Type}) deserialize edilemedi (payload boş/geçersiz).");

        await eventBus.PublishAsync(integrationEvent, cancellationToken);
    }
}
