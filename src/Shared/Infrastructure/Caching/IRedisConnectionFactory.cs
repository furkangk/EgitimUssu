using StackExchange.Redis;

namespace EgitimUssu.Shared.Infrastructure.Caching;

public interface IRedisConnectionFactory
{
    ValueTask<IConnectionMultiplexer> GetAsync(CancellationToken cancellationToken = default);
}
