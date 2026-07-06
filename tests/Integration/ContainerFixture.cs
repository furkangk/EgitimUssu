using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// M14: Gerçek Postgres + Redis container'ları (Testcontainers). Docker yoksa <see cref="Available"/> false olur
/// ve testler <c>Assert.SkipUnless</c> ile atlanır (hızlı InMemory paketi Docker'sız da çalışır).
/// </summary>
public sealed class ContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RedisContainer? _redis;

    public bool Available { get; private set; }

    public string PostgresConnectionString { get; private set; } = string.Empty;

    public string RedisConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
            _redis = new RedisBuilder("redis:7-alpine").Build();

            await _postgres.StartAsync();
            await _redis.StartAsync();

            PostgresConnectionString = _postgres.GetConnectionString();
            RedisConnectionString = _redis.GetConnectionString();
            Available = true;
        }
        catch
        {
            // Docker erişilemez: testler skip edilecek.
            Available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }

        if (_redis is not null)
        {
            await _redis.DisposeAsync();
        }
    }
}

[CollectionDefinition("containers")]
public sealed class ContainerCollection : ICollectionFixture<ContainerFixture>;
