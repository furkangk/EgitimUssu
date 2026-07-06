namespace EgitimUssu.Tests.Integration;

/// <summary>
/// M14: Uygulamayı gerçek Postgres/Redis'e yöneltmek için ortam değişkenlerini geçici olarak ayarlar
/// (bu kod tabanının mevcut integration testlerindeki kanıtlanmış desen). Dispose'da eski değerleri geri yükler.
/// </summary>
internal static class RealInfrastructure
{
    public static IDisposable Use(
        ContainerFixture fixture,
        bool applyMigrations = true,
        bool dispatchOutbox = false,
        int? maxRetryCount = null)
    {
        return new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["ConnectionStrings__Postgres"] = fixture.PostgresConnectionString,
            ["Redis__Configuration"] = fixture.RedisConnectionString,
            ["Database__ApplyMigrationsOnStartup"] = applyMigrations ? "true" : "false",
            ["Outbox__DispatchEnabled"] = dispatchOutbox ? "true" : "false",
            ["Outbox__MaxRetryCount"] = maxRetryCount?.ToString(),
        });
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new();

        public EnvironmentVariableScope(IReadOnlyDictionary<string, string?> values)
        {
            foreach (var (key, value) in values)
            {
                _previous[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
