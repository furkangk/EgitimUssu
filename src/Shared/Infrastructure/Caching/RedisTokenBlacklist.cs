using StackExchange.Redis;

namespace EgitimUssu.Shared.Infrastructure.Caching;

/// <summary>Y4: Redis tabanlı token blacklist. Anahtar, token'ın kalan ömrü kadar TTL ile tutulur.</summary>
public sealed class RedisTokenBlacklist(ResilientRedisExecutor redis) : ITokenBlacklist
{
    /// <summary>A-01: Redis düştüğünde hangi korumanın fail-open geçtiğini ölçen etiket.</summary>
    private const string Protection = "blacklist";

    private const string KeyPrefix = "token-blacklist:";

    /// <summary>A-06: Kullanıcı bazlı iptal damgası — "şu andan önce verilen token'lar geçersiz".</summary>
    private const string UserKeyPrefix = "token-valid-after:";

    /// <summary>P15 T7 / N-31: Kapatılmış oturum — bu <c>sid</c>'den verilmiş token'lar geçersiz.</summary>
    private const string SessionKeyPrefix = "session-revoked:";

    public Task BlacklistAsync(string tokenId, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenId) || timeToLive <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        return redis.TryExecuteAsync(
            Protection,
            database => database.StringSetAsync(Key(tokenId), "1", timeToLive), cancellationToken);
    }

    public async Task<bool> IsBlacklistedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return false;
        }

        var (ok, exists) = await redis.TryExecuteAsync(
            Protection,
            database => database.KeyExistsAsync(Key(tokenId)), cancellationToken);
        return ok && exists; // fail-open: Redis erişilemezse token'ı geçerli say
    }

    /// <inheritdoc />
    public Task RevokeUserTokensIssuedBeforeAsync(
        string userId,
        DateTime cutoffUtc,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || timeToLive <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        // P15 T7 / N-32: Damga milisaniye çözünürlüğündedir. Eskiden saniyeydi ve `iat` (saniye) ile
        // `<` karşılaştırılıyordu: iptalle aynı saniyede verilmiş token geçiyordu. `<=` de çözüm
        // değildi — iptalden hemen sonra (aynı saniyede) yeniden giren kullanıcının yeni token'ını
        // reddederdi. Token veriliş anını `iat_ms` talebiyle taşır.
        var cutoff = ToUnixMilliseconds(cutoffUtc);

        return redis.TryExecuteAsync(
            Protection,
            database => database.StringSetAsync(UserKey(userId), cutoff, timeToLive),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task RevokeSessionsAsync(
        IReadOnlyCollection<string> sessionIds,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        var ids = sessionIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
        if (ids.Length == 0 || timeToLive <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        // Oturum başına bir anahtar (SET üyelerine ayrı TTL verilemez); yazımlar pipeline'lanır.
        return redis.TryExecuteAsync(
            Protection,
            async database =>
            {
                await Task.WhenAll(ids.Select(id => database.StringSetAsync(SessionKey(id), "1", timeToLive)));
                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsRevokedAsync(
        string tokenId,
        string? userId,
        string? sessionId,
        DateTime? issuedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var checkStamp = !string.IsNullOrWhiteSpace(userId) && issuedAtUtc.HasValue;
        var checkSession = !string.IsNullOrWhiteSpace(sessionId);
        if (string.IsNullOrWhiteSpace(tokenId) && !checkStamp && !checkSession)
        {
            return false;
        }

        // Okumalar tek `TryExecuteAsync` içinde başlatılır: StackExchange.Redis bunları
        // pipeline'lar, dolayısıyla istek başına ek bir gidiş-dönüş oluşmaz.
        var (ok, result) = await redis.TryExecuteAsync(
            Protection,
            async database =>
            {
                var blacklisted = string.IsNullOrWhiteSpace(tokenId)
                    ? Task.FromResult(false)
                    : database.KeyExistsAsync(Key(tokenId));
                var sessionRevoked = checkSession
                    ? database.KeyExistsAsync(SessionKey(sessionId!))
                    : Task.FromResult(false);
                var stamp = checkStamp
                    ? database.StringGetAsync(UserKey(userId!))
                    : Task.FromResult(RedisValue.Null);

                await Task.WhenAll(blacklisted, sessionRevoked, stamp);
                return (Blacklisted: await blacklisted, SessionRevoked: await sessionRevoked, Stamp: await stamp);
            },
            cancellationToken);

        // fail-open: Redis erişilemezse token'ı geçerli say (ADR-0004; düşüş A-01 sayacında görünür).
        if (!ok)
        {
            return false;
        }

        if (result.Blacklisted || result.SessionRevoked)
        {
            return true;
        }

        if (!checkStamp || result.Stamp.IsNullOrEmpty || !result.Stamp.TryParse(out long cutoff))
        {
            return false;
        }

        return ToUnixMilliseconds(issuedAtUtc!.Value) < cutoff;
    }

    private static long ToUnixMilliseconds(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static RedisKey Key(string tokenId) => new($"{KeyPrefix}{tokenId}");

    private static RedisKey UserKey(string userId) => new($"{UserKeyPrefix}{userId}");

    private static RedisKey SessionKey(string sessionId) => new($"{SessionKeyPrefix}{sessionId}");
}
