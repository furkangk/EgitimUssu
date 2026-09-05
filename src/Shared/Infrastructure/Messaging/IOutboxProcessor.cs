namespace EgitimUssu.Shared.Infrastructure.Messaging;

public interface IOutboxProcessor
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>A-09: Islenmis outbox/inbox satirlarindan retention suresinden eskilerini siler.</summary>
    /// <param name="retention">Bu sureden eski islenmis satirlar silinir.</param>
    /// <param name="cancellationToken">Iptal belirteci.</param>
    /// <returns>Silinen satir sayisi.</returns>
    Task<int> PurgeProcessedAsync(TimeSpan retention, CancellationToken cancellationToken = default);
}
