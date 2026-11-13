using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Health;

public sealed class ConfigurationHealthCheck(
    IConfiguration configuration,
    IHostEnvironment environment,
    IOptions<RedisOptions> redisOptions,
    IOptions<JwtOptions> jwtOptions,
    IReadOnlyCollection<IModule> modules) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();

        var connectionStringIssue = ConnectionStringGuard.Validate(
            configuration.GetConnectionString("Postgres"),
            environment.IsDevelopment());
        if (connectionStringIssue is not null)
        {
            issues.Add(connectionStringIssue);
        }

        // A-01: Eskiden yalnız "boş mu" diye bakılıyordu. Varsayılan "localhost:6379" boş olmadığı
        // için, ortam değişkeni verilmemiş bir üretim dağıtımı bu kontrolden geçiyor ve dört koruma
        // birden sessizce kapalıyken sağlık uçları "Healthy" diyordu. Guard loopback'i de reddeder.
        var redisIssue = RedisOptionsGuard.Validate(redisOptions.Value, environment.IsDevelopment());
        if (redisIssue is not null)
        {
            issues.Add(redisIssue);
        }

        var jwtKeyIssue = JwtSigningKeyGuard.Validate(jwtOptions.Value.SigningKey);
        if (jwtKeyIssue is not null)
        {
            issues.Add(jwtKeyIssue);
        }

        if (modules.Count == 0)
        {
            issues.Add("No modules registered.");
        }

        return Task.FromResult(
            issues.Count == 0
                ? HealthCheckResult.Healthy("Configuration is valid.")
                : HealthCheckResult.Unhealthy(string.Join(" ", issues)));
    }
}
