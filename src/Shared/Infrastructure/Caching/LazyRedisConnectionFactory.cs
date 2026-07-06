using StackExchange.Redis;

namespace EgitimUssu.Shared.Infrastructure.Caching;

public sealed class LazyRedisConnectionFactory : IRedisConnectionFactory
{
    // Redis erişilemezse, her istekte yeniden bağlanmayı denemek yerine kısa süre hızlıca hata ver
    // (bağlantı kilidinde sıraya girip tıkanmayı önler; ResilientRedisExecutor fail-open'a düşer).
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _configuration;
    private IConnectionMultiplexer? _connection;
    private DateTime _nextAttemptUtc = DateTime.MinValue;

    public LazyRedisConnectionFactory(string configuration)
    {
        _configuration = configuration;
    }

    public async ValueTask<IConnectionMultiplexer> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        ThrowIfInCooldown();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null)
            {
                return _connection;
            }

            ThrowIfInCooldown();

            try
            {
                _connection = await ConnectionMultiplexer.ConnectAsync(BuildOptions());
                return _connection;
            }
            catch
            {
                // Sonraki istekler bağlantıyı yeniden denemesin; hızlıca fail-open olsun.
                _nextAttemptUtc = DateTime.UtcNow.Add(FailureCooldown);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private void ThrowIfInCooldown()
    {
        if (DateTime.UtcNow < _nextAttemptUtc)
        {
            throw new RedisConnectionException(
                ConnectionFailureType.UnableToConnect,
                "Redis geçici olarak devre dışı (bağlantı cooldown).");
        }
    }

    private ConfigurationOptions BuildOptions()
    {
        var options = ConfigurationOptions.Parse(_configuration);
        // Redis yoksa hızlıca hata ver (uzun bekleme yok) — graceful degradation.
        options.AbortOnConnectFail = true;
        options.ConnectTimeout = 1000;
        options.ConnectRetry = 1;
        options.SyncTimeout = 1000;
        return options;
    }
}
