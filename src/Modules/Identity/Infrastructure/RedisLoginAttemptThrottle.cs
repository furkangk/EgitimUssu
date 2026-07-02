using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Shared.Infrastructure.Caching;
using StackExchange.Redis;

namespace EgitimUssu.Modules.Identity.Infrastructure;

/// <summary>
/// Y4: Redis tabanlı hesap-bazlı giriş kilidi. Ardışık <see cref="MaxFailedAttempts"/> başarısız denemeden sonra
/// hesap <see cref="Window"/> süresince kilitlenir. Redis erişilemezse <b>fail-open</b> (kilitlemez) — ADR-0004.
/// </summary>
internal sealed class RedisLoginAttemptThrottle(ResilientRedisExecutor redis) : ILoginAttemptThrottle
{
    private const int MaxFailedAttempts = 5;
    private const string KeyPrefix = "login-fail:";
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public async Task<bool> IsLockedAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var (ok, value) = await redis.TryExecuteAsync(
            database => database.StringGetAsync(Key(normalizedEmail)),
            cancellationToken);

        if (!ok || value.IsNullOrEmpty)
        {
            return false; // fail-open (Redis erişilemez veya kayıt yok)
        }

        return value.TryParse(out long count) && count >= MaxFailedAttempts;
    }

    public Task RecordFailureAsync(string normalizedEmail, CancellationToken cancellationToken)
        => redis.TryExecuteAsync(
            async database =>
            {
                var key = Key(normalizedEmail);
                var count = await database.StringIncrementAsync(key);
                if (count == 1)
                {
                    await database.KeyExpireAsync(key, Window);
                }

                return count;
            },
            cancellationToken);

    public Task ResetAsync(string normalizedEmail, CancellationToken cancellationToken)
        => redis.TryExecuteAsync(database => database.KeyDeleteAsync(Key(normalizedEmail)), cancellationToken);

    private static RedisKey Key(string normalizedEmail) => new($"{KeyPrefix}{normalizedEmail}");
}
