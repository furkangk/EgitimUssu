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

        // Damga saniye çözünürlüğünde tutulur; `iat` claim'i de Unix saniyesidir, aynı ölçekte
        // karşılaştırılmaları gerekir.
        var cutoff = new DateTimeOffset(cutoffUtc, TimeSpan.Zero).ToUnixTimeSeconds();

        return redis.TryExecuteAsync(
            Protection,
            database => database.StringSetAsync(UserKey(userId), cutoff, timeToLive),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsRevokedAsync(
        string tokenId,
        string? userId,
        DateTime? issuedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var checkStamp = !string.IsNullOrWhiteSpace(userId) && issuedAtUtc.HasValue;
        if (string.IsNullOrWhiteSpace(tokenId) && !checkStamp)
        {
            return false;
        }

        // İki okuma tek `TryExecuteAsync` içinde başlatılır: StackExchange.Redis bunları
        // pipeline'lar, dolayısıyla istek başına ek bir gidiş-dönüş oluşmaz.
        var (ok, result) = await redis.TryExecuteAsync(
            Protection,
            async database =>
            {
                var blacklisted = string.IsNullOrWhiteSpace(tokenId)
                    ? Task.FromResult(false)
                    : database.KeyExistsAsync(Key(tokenId));
                var stamp = checkStamp
                    ? database.StringGetAsync(UserKey(userId!))
                    : Task.FromResult(RedisValue.Null);

                await Task.WhenAll(blacklisted, stamp);
                return (Blacklisted: await blacklisted, Stamp: await stamp);
            },
            cancellationToken);

        // fail-open: Redis erişilemezse token'ı geçerli say (ADR-0004; düşüş A-01 sayacında görünür).
        if (!ok)
        {
            return false;
        }

        if (result.Blacklisted)
        {
            return true;
        }

        if (!checkStamp || result.Stamp.IsNullOrEmpty || !result.Stamp.TryParse(out long cutoff))
        {
            return false;
        }

        var issuedAt = new DateTimeOffset(issuedAtUtc!.Value, TimeSpan.Zero).ToUnixTimeSeconds();
        return issuedAt < cutoff;
    }

    private static RedisKey Key(string tokenId) => new($"{KeyPrefix}{tokenId}");

    private static RedisKey UserKey(string userId) => new($"{UserKeyPrefix}{userId}");
}
