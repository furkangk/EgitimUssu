using System.Text.Json;
using StackExchange.Redis;

namespace EgitimUssu.Shared.Infrastructure.Caching;

/// <summary>Y4: Redis tabanlı idempotency deposu. "İşleniyor" işaretini kısa TTL ile atomik (SET NX) koyar; tamamlanınca yanıtı saklar.</summary>
public sealed class RedisIdempotencyStore(ResilientRedisExecutor redis) : IIdempotencyStore
{
    /// <summary>A-01: Redis düştüğünde hangi korumanın fail-open geçtiğini ölçen etiket.</summary>
    private const string Protection = "idempotency";

    private const string KeyPrefix = "idempotency:";
    private const string ProcessingMarker = "processing";
    /// <summary>
    /// A-12: "İşleniyor" işaretinin ömrü. Eskiden 30 saniyeydi ve istek sürerken yenilenmiyordu:
    /// 30 saniyeyi aşan bir istek (S3 yüklemesi, soğuk başlangıç) devam ederken işaret düşüyor,
    /// aynı anahtarla gelen tekrar <c>Proceed</c> alıyor ve iş <b>iki kez</b> koşuyordu — koruma
    /// tam da kullanıcının tekrar denediği yavaş isteklerde bozuluyordu.
    /// <para>
    /// Süre artık makul her istek süresini kapsar. Normal akışta bu TTL zaten beklenmez: başarıda
    /// <see cref="CompleteAsync"/> anahtarın üzerine yazar, başarısızlıkta <see cref="ReleaseAsync"/>
    /// siler. TTL yalnız süreç isteğin ortasında çökerse devreye girer — o durumda işin yazılıp
    /// yazılmadığı bilinemediği için bir süre bloklamak doğru davranıştır.
    /// </para>
    /// </summary>
    private static readonly TimeSpan ProcessingTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CompletedTtl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(IdempotencyOutcome Outcome, IdempotentResponse? StoredResponse)> TryBeginAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var redisKey = Key(key);

        // Atomik sahiplenme: yalnız anahtar yoksa "işleniyor" işaretini koy.
        var (ok, created) = await redis.TryExecuteAsync(
            Protection,
            database => database.StringSetAsync(redisKey, ProcessingMarker, ProcessingTtl, When.NotExists),
            cancellationToken);

        if (!ok || created)
        {
            return (IdempotencyOutcome.Proceed, null); // fail-open (Redis yok) veya ilk istek
        }

        var (okGet, value) = await redis.TryExecuteAsync(
            Protection,
            database => database.StringGetAsync(redisKey), cancellationToken);
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
        return redis.TryExecuteAsync(
            Protection,
            database => database.StringSetAsync(Key(key), payload, CompletedTtl), cancellationToken);
    }

    /// <inheritdoc />
    public Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
        // Yalnız başarısız akışta çağrılır; başarılı akışta anahtarın üzerine zaten saklı yanıt
        // yazılmış olur, dolayısıyla burada tamamlanmış bir kaydı silme riski yoktur.
        => redis.TryExecuteAsync(
            Protection,
            database => database.KeyDeleteAsync(Key(key)), cancellationToken);

    private static RedisKey Key(string key) => new($"{KeyPrefix}{key}");
}
