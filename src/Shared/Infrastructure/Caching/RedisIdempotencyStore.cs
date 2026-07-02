using System.Text.Json;
using StackExchange.Redis;

namespace EgitimUssu.Shared.Infrastructure.Caching;

/// <summary>Y4: Redis tabanlı idempotency deposu. "İşleniyor" işaretini kısa TTL ile atomik (SET NX) koyar; tamamlanınca yanıtı saklar.</summary>
public sealed class RedisIdempotencyStore(ResilientRedisExecutor redis) : IIdempotencyStore
{
    private const string KeyPrefix = "idempotency:";
    private const string ProcessingMarker = "processing";
    private static readonly TimeSpan ProcessingTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CompletedTtl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(IdempotencyOutcome Outcome, IdempotentResponse? StoredResponse)> TryBeginAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var redisKey = Key(key);

        // Atomik sahiplenme: yalnız anahtar yoksa "işleniyor" işaretini koy.
        var (ok, created) = await redis.TryExecuteAsync(
            database => database.StringSetAsync(redisKey, ProcessingMarker, ProcessingTtl, When.NotExists),
            cancellationToken);

        if (!ok || created)
        {
            return (IdempotencyOutcome.Proceed, null); // fail-open (Redis yok) veya ilk istek
        }

        var (okGet, value) = await redis.TryExecuteAsync(database => database.StringGetAsync(redisKey), cancellationToken);
        if (!okGet || value.IsNullOrEmpty)
        {
            return (IdempotencyOutcome.Proceed, null); // yarış/expire → işle
        }

        var payload = value.ToString();
        if (payload == ProcessingMarker)
        {
            return (IdempotencyOutcome.InProgress, null);
        }

        var stored = JsonSerializer.Deserialize<IdempotentResponse>(payload, JsonOptions);
        return stored is null
            ? (IdempotencyOutcome.Proceed, null)
            : (IdempotencyOutcome.Duplicate, stored);
    }

    public Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(response, JsonOptions);
        return redis.TryExecuteAsync(database => database.StringSetAsync(Key(key), payload, CompletedTtl), cancellationToken);
    }

    private static RedisKey Key(string key) => new($"{KeyPrefix}{key}");
}
