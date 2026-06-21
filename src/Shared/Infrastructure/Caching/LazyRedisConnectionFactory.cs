using StackExchange.Redis;

namespace EgitimUssu.Shared.Infrastructure.Caching;

public sealed class LazyRedisConnectionFactory : IRedisConnectionFactory
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _configuration;
    private IConnectionMultiplexer? _connection;

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

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _connection ??= await ConnectionMultiplexer.ConnectAsync(_configuration);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }
}
