using StackExchange.Redis;

namespace EgitimUssu.Shared.Infrastructure.Caching;

/// <summary>Y4: Redis tabanlı token blacklist. Anahtar, token'ın kalan ömrü kadar TTL ile tutulur.</summary>
public sealed class RedisTokenBlacklist(ResilientRedisExecutor redis) : ITokenBlacklist
{
    private const string KeyPrefix = "token-blacklist:";

    public Task BlacklistAsync(string tokenId, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenId) || timeToLive <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        return redis.TryExecuteAsync(database => database.StringSetAsync(Key(tokenId), "1", timeToLive), cancellationToken);
    }

    public async Task<bool> IsBlacklistedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return false;
        }

        var (ok, exists) = await redis.TryExecuteAsync(database => database.KeyExistsAsync(Key(tokenId)), cancellationToken);
        return ok && exists; // fail-open: Redis erişilemezse token'ı geçerli say
    }

    private static RedisKey Key(string tokenId) => new($"{KeyPrefix}{tokenId}");
}
