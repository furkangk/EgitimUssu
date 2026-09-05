using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Health;

/// <summary>
/// A-01: Redis'e gerçekten ulaşılabildiğini doğrular.
/// <para>
/// <see cref="ConfigurationHealthCheck"/> yalnız ayarın <i>varlığına</i> bakar; erişilebilirliğe bakmaz.
/// Redis'e bağlı korumalar fail-open olduğu için bağlantı koptuğunda hiçbir istek hata vermez —
/// yani bu kontrol olmadan "hız sınırı, token kara listesi, giriş kilidi ve idempotency kapalı"
/// durumunun dışarıdan görülebilir hiçbir izi kalmaz.
/// </para>
/// <para>
/// Sonuç bilinçli olarak <see cref="HealthStatus.Degraded"/>: fail-open bir tasarım kararıdır
/// (ADR-0004), dolayısıyla Redis kesintisi uygulamayı <b>öldürmemelidir</b>. Degraded, <c>/health/ready</c>
/// gövdesinde görünür ve 200 döndürür — orkestratör konteyneri yeniden başlatmaz, ekip durumu görür.
/// </para>
/// </summary>
public sealed class RedisConnectionHealthCheck(
    IRedisConnectionFactory connectionFactory,
    IOptions<RedisOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Configuration))
        {
            return HealthCheckResult.Degraded(
                "Redis:Configuration missing; rate limiting, token blacklist, login lockout and idempotency are disabled.");
        }

        try
        {
            var connection = await connectionFactory.GetAsync(cancellationToken);
            await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy("Redis connection succeeded.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Degraded(
                "Redis unreachable; rate limiting, token blacklist, login lockout and idempotency are failing open.",
                exception);
        }
    }
}
