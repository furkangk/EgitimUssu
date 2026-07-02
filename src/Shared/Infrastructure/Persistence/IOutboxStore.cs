namespace EgitimUssu.Shared.Infrastructure.Persistence;

public interface IOutboxStore
{
    /// <summary>
    /// Bekleyen outbox mesajlarını claim eder ve <paramref name="publishAsync"/> ile yayınlar.
    /// K5: Her mesaj tek tek işlenir; başarısızlık diğerlerini bloklamaz (retry/backoff/dead-letter).
    /// Çoklu-instance'ta Npgsql için <c>FOR UPDATE SKIP LOCKED</c> ile satır sahiplenme uygulanır.
    /// </summary>
    /// <returns>Başarıyla yayınlanan mesaj sayısı.</returns>
    Task<int> ProcessPendingAsync(
        Func<OutboxBatchItem, CancellationToken, Task> publishAsync,
        CancellationToken cancellationToken = default);
}
