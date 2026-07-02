using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EgitimUssu.Shared.Infrastructure.Caching;

/// <summary>
/// Y4: Redis işlemlerini dayanıklı (graceful-degradation) biçimde çalıştırır. Redis erişilemezse
/// kısa bir cooldown boyunca bağlantı denemesi yapılmaz (connection-storm önleme) ve işlem
/// "başarısız" (<c>ok=false</c>) döner — çağıran taraf fail-open/fail-closed kararını kendisi verir.
/// </summary>
public sealed class ResilientRedisExecutor(
    IRedisConnectionFactory connectionFactory,
    IClock clock,
    ILogger<ResilientRedisExecutor> logger)
{
    private static readonly TimeSpan UnavailableCooldown = TimeSpan.FromSeconds(10);
    private readonly object _gate = new();
    private DateTime _unavailableUntilUtc = DateTime.MinValue;

    public bool IsTemporarilyUnavailable
    {
        get
        {
            lock (_gate)
            {
                return clock.UtcNow < _unavailableUntilUtc;
            }
        }
    }

    /// <summary>İşlemi çalıştırır. Redis erişilemez/hatalıysa <c>(false, default)</c> döner (istisna fırlatmaz).</summary>
    public async Task<(bool Ok, T? Value)> TryExecuteAsync<T>(
        Func<IDatabase, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (IsTemporarilyUnavailable)
        {
            return (false, default);
        }

        try
        {
            var connection = await connectionFactory.GetAsync(cancellationToken);
            var database = connection.GetDatabase();
            return (true, await operation(database));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkUnavailable(exception);
            return (false, default);
        }
    }

    private void MarkUnavailable(Exception exception)
    {
        lock (_gate)
        {
            _unavailableUntilUtc = clock.UtcNow.Add(UnavailableCooldown);
        }

        logger.LogWarning(
            exception,
            "Redis erişilemez; {Seconds}s boyunca devre dışı bırakıldı (graceful degradation).",
            UnavailableCooldown.TotalSeconds);
    }
}
