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

        if (string.IsNullOrWhiteSpace(redisOptions.Value.Configuration))
        {
            issues.Add("Redis:Configuration missing.");
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
