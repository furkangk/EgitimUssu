namespace EgitimUssu.Shared.Infrastructure.Caching;

public enum IdempotencyOutcome
{
    /// <summary>Bu anahtar ilk kez görülüyor (veya Redis erişilemez) — istek işlenmeli.</summary>
    Proceed,

    /// <summary>Aynı anahtarla tamamlanmış bir istek var — saklı yanıt tekrar oynatılmalı.</summary>
    Duplicate,

    /// <summary>Aynı anahtarla eşzamanlı bir istek hâlâ işleniyor — 409 dönülmeli.</summary>
    InProgress
}

/// <summary>Idempotency için saklanan HTTP yanıtı.</summary>
public sealed record IdempotentResponse(int StatusCode, string? ContentType, byte[] Body);

/// <summary>
/// Y4: Mutasyon uçlarında <c>Idempotency-Key</c> ile tekrarlı isteklerin güvenli tekrarını sağlar.
/// Redis erişilemezse fail-open (dedup yapılmaz, istek normal işlenir).
/// </summary>
public interface IIdempotencyStore
{
    Task<(IdempotencyOutcome Outcome, IdempotentResponse? StoredResponse)> TryBeginAsync(string key, CancellationToken cancellationToken = default);

    Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken = default);
}
