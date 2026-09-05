namespace EgitimUssu.Shared.Infrastructure.Persistence;

/// <summary>A-07: Arka plan işi kirası — aynı işin aynı anda tek örnekte koşmasını sağlar.</summary>
public interface IJobLeaseStore
{
    /// <summary>
    /// Kirayı almaya çalışır. Kira başkasındaysa ve süresi dolmamışsa <c>false</c> döner.
    /// </summary>
    /// <param name="dbContext">İşin ait olduğu modülün context'i (kira o şemada tutulur).</param>
    /// <param name="jobName">İşin benzersiz adı.</param>
    /// <param name="leaseDuration">Kiranın geçerlilik süresi; bir tur süresinden uzun olmalı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Kira alındıysa <c>true</c>.</returns>
    Task<bool> TryAcquireAsync(
        ModuleDbContext dbContext,
        string jobName,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
}
