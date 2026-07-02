namespace EgitimUssu.Shared.Infrastructure.Caching;

public interface IRateLimiter
{
    /// <summary>
    /// Sabit pencere (fixed-window) sayacıyla bir izin talep eder.
    /// </summary>
    /// <returns><c>true</c> = izin verildi; <c>false</c> = limit aşıldı.</returns>
    Task<bool> TryAcquireAsync(string partitionKey, int permitLimit, TimeSpan window, CancellationToken cancellationToken = default);
}
