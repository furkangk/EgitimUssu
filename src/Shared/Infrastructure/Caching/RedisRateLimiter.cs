using EgitimUssu.Shared.Kernel;
using StackExchange.Redis;

namespace EgitimUssu.Shared.Infrastructure.Caching;

/// <summary>
/// Y4: Redis tabanlı dağıtık sabit-pencere rate limiter. Çoklu-instance'ta tek doğru sayaç sağlar.
/// Redis erişilemezse <b>fail-open</b> davranır (istek geçer) — ADR-0004 kararı.
/// </summary>
public sealed class RedisRateLimiter(ResilientRedisExecutor redis, IClock clock) : IRateLimiter
{
    private const string KeyPrefix = "ratelimit:";

    public async Task<bool> TryAcquireAsync(
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        if (permitLimit <= 0 || window <= TimeSpan.Zero)
        {
            return true;
        }

        // Pencereyi zaman eksenine sabitle: aynı penceredeki tüm istekler aynı anahtarı paylaşır.
        var windowTicks = window.Ticks;
        var windowIndex = clock.UtcNow.Ticks / windowTicks;
        var redisKey = new RedisKey($"{KeyPrefix}{partitionKey}:{windowIndex}");

        var (ok, count) = await redis.TryExecuteAsync(
            async database =>
            {
                var value = await database.StringIncrementAsync(redisKey);
                if (value == 1)
                {
                    // İlk artışta pencere süresini ata (küçük tampon ile).
                    await database.KeyExpireAsync(redisKey, window + TimeSpan.FromSeconds(1));
                }

                return value;
            },
            cancellationToken);

        // Fail-open: Redis erişilemezse (ok == false) isteğe izin ver.
        return !ok || count <= permitLimit;
    }
}
