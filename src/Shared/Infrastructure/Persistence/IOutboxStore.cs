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

    /// <summary>
    /// A-09: İşlenmiş outbox ve inbox satırlarından <paramref name="retention"/> süresinden eskilerini siler.
    /// <para>
    /// Her modülün kendi <c>outbox_messages</c> ve <c>inbox_messages</c> tablosu vardı ve hiçbiri
    /// budanmıyordu: satırlar işlendi diye işaretleniyor ama duruyordu. Dead-letter'a düşmüş
    /// satırlara <b>dokunulmaz</b> — onlar incelenmek için oradadır.
    /// </para>
    /// </summary>
    /// <param name="retention">Bu süreden eski işlenmiş satırlar silinir.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Silinen satır sayısı.</returns>
    Task<int> PurgeProcessedAsync(TimeSpan retention, CancellationToken cancellationToken = default);
}
