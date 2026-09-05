using EgitimUssu.Shared.Infrastructure.Observability;
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
    EgitimUssuMetrics metrics,
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

    /// <summary>
    /// İşlemi çalıştırır. Redis erişilemez/hatalıysa <c>(false, default)</c> döner (istisna fırlatmaz).
    /// </summary>
    /// <typeparam name="T">İşlemin dönüş tipi.</typeparam>
    /// <param name="protection">A-01: Fail-open'a düşüldüğünde hangi korumanın kapandığını ölçmek için
    /// kullanılan ad (`ratelimit` | `blacklist` | `login-throttle` | `idempotency`). Sessiz devre dışı
    /// kalma tek dışa vurumu bu sayaç olduğu için parametre <b>zorunludur</b>.</param>
    /// <param name="operation">Redis üzerinde çalıştırılacak işlem.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Başarı bayrağı ve sonuç.</returns>
    public async Task<(bool Ok, T? Value)> TryExecuteAsync<T>(
        string protection,
        Func<IDatabase, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (IsTemporarilyUnavailable)
        {
            // Cooldown penceresindeki her çağrı da korumasız geçiyor; sayaç bunu da görmeli,
            // yoksa kesinti süresince yalnız ilk hata ölçülür ve alarm gerçek hacmi göremez.
            metrics.RecordRedisUnavailable(protection);
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
            metrics.RecordRedisUnavailable(protection);
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
