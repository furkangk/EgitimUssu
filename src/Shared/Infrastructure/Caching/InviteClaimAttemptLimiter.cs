using EgitimUssu.Shared.Application;
using StackExchange.Redis;

namespace EgitimUssu.Shared.Infrastructure.Caching;

/// <summary>
/// P15 / N-20: Redis tabanlı, kullanıcı başına davet kodu deneme kilidi — saatte 10 başarısız deneme.
/// <para>Redis erişilemezse <b>fail-open</b> (kilitlemez) — ADR-0004; düşüş A-01 sayacıyla ölçülür.</para>
/// </summary>
public sealed class InviteClaimAttemptLimiter(ResilientRedisExecutor redis) : IInviteClaimAttemptLimiter
{
    /// <summary>Kilitlenmeden önce izin verilen başarısız deneme.</summary>
    public const int MaxFailedAttempts = 10;

    /// <summary>Sayaç penceresi; ilk başarısız denemeden itibaren işler.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private const string Protection = "invite-claim-throttle";
    private const string KeyPrefix = "invite-claim-fail:";

    /// <inheritdoc />
    public async Task<bool> IsBlockedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var (ok, value) = await redis.TryExecuteAsync(
            Protection,
            database => database.StringGetAsync(Key(userId)),
            cancellationToken);

        return ok && !value.IsNullOrEmpty && value.TryParse(out long count) && count >= MaxFailedAttempts;
    }

    /// <inheritdoc />
    public Task RecordFailureAsync(Guid userId, CancellationToken cancellationToken)
        => redis.TryExecuteAsync(
            Protection,
            async database =>
            {
                var count = await database.StringIncrementAsync(Key(userId));
                if (count == 1)
                {
                    await database.KeyExpireAsync(Key(userId), Window);
                }

                return true;
            },
            cancellationToken);

    /// <inheritdoc />
    public Task ResetAsync(Guid userId, CancellationToken cancellationToken)
        => redis.TryExecuteAsync(
            Protection,
            database => database.KeyDeleteAsync(Key(userId)),
            cancellationToken);

    private static RedisKey Key(Guid userId) => new($"{KeyPrefix}{userId:N}");
}
