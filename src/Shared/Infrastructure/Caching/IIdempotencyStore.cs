namespace EgitimUssu.Shared.Infrastructure.Caching;

public enum IdempotencyOutcome
{
    /// <summary>Bu anahtar ilk kez görülüyor (veya Redis erişilemez) — istek işlenmeli.</summary>
    Proceed,

    /// <summary>Aynı anahtarla tamamlanmış bir istek var — saklı yanıt tekrar oynatılmalı.</summary>
    Duplicate,

    /// <summary>Aynı anahtarla eşzamanlı bir istek hâlâ işleniyor — 409 dönülmeli.</summary>
    InProgress,

    /// <summary>Anahtar farklı bir istek gövdesine ait — 422 dönülmeli.</summary>
    BodyMismatch
}

/// <summary>Idempotency için saklanan HTTP yanıtı.</summary>
public sealed record IdempotentResponse(int StatusCode, string? ContentType, byte[] Body, string? RequestBodyHash = null);

/// <summary>
/// Y4: Mutasyon uçlarında <c>Idempotency-Key</c> ile tekrarlı isteklerin güvenli tekrarını sağlar.
/// Redis erişilemezse fail-open (dedup yapılmaz, istek normal işlenir).
/// </summary>
public interface IIdempotencyStore
{
    Task<(IdempotencyOutcome Outcome, IdempotentResponse? StoredResponse)> TryBeginAsync(string key, string requestBodyHash, CancellationToken cancellationToken = default);

    Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken = default);

    /// <summary>
    /// A-12: "İşleniyor" işaretini kaldırır — istek başarısız bittiğinde (2xx dışı yanıt ya da
    /// istisna) çağrılır. Olmadığında başarısız bir istek anahtarı TTL boyunca kilitli bırakıyor,
    /// kullanıcının aynı <c>Idempotency-Key</c> ile yaptığı yeniden deneme <c>409</c> alıyordu —
    /// yani koruma, tam da tekrar denenmesi gereken durumda işi engelliyordu.
    /// </summary>
    /// <param name="key">Depo anahtarı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>İşlem görevi.</returns>
    Task ReleaseAsync(string key, CancellationToken cancellationToken = default);
}
