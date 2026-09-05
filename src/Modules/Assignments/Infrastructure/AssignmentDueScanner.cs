using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

/// <summary>M06-3: Bir tarama turunda üretilen uyarı sayıları.</summary>
/// <param name="DueSoonCount">"Teslime az kaldı" uyarısı üretilen ödev sayısı.</param>
/// <param name="MissedCount">"Teslim edilmedi" uyarısı üretilen ödev sayısı.</param>
public sealed record AssignmentDueScanResult(int DueSoonCount, int MissedCount)
{
    public int Total => DueSoonCount + MissedCount;
}

/// <summary>M06-3: Son teslim uyarılarını üreten tarama turu (arka plan servisinden ayrık, test edilebilir).</summary>
public interface IAssignmentDueScanProcessor
{
    /// <summary>Bir tarama turu koşturur.</summary>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Bu turda üretilen uyarı sayıları.</returns>
    Task<AssignmentDueScanResult> ScanAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// M06-3: Son teslimine <see cref="DueSoonWindow"/> kalan ve hâlâ teslim edilmemiş ödevler için
/// <see cref="AssignmentDueSoonDomainEvent"/>, vadesi geçip teslim edilmemişler için
/// <see cref="AssignmentMissedDomainEvent"/> üretir. Tekrarı domain engeller
/// (<c>DueSoonNotifiedOnUtc</c>/<c>MissedNotifiedOnUtc</c>), dolayısıyla aynı ödev için ikinci olay çıkmaz.
/// Bildirim burada gönderilmez — olaylar outbox'a düşer, Notifications tüketir (Öğrenilenler #40).
/// </summary>
public sealed class AssignmentDueScanProcessor : IAssignmentDueScanProcessor
{
    /// <summary>Uyarının ne kadar önce üretileceği (plan: 24 saat).</summary>
    private static readonly TimeSpan DueSoonWindow = TimeSpan.FromHours(24);

    /// <summary>Tek turda işlenecek ödev üst sınırı (tur süresini sınırlar).</summary>
    private const int BatchSize = 200;

    private readonly AssignmentsDbContext _dbContext;
    private readonly IClock _clock;

    public AssignmentDueScanProcessor(AssignmentsDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<AssignmentDueScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var horizon = now + DueSoonWindow;

        var candidates = await _dbContext.Assignments
            .Where(assignment => assignment.DueDateUtc != null
                && assignment.DueDateUtc <= horizon
                && (assignment.Status == AssignmentStatus.Pending
                    || assignment.Status == AssignmentStatus.ReturnedForRevision)
                && (assignment.DueSoonNotifiedOnUtc == null || assignment.MissedNotifiedOnUtc == null))
            .OrderBy(assignment => assignment.DueDateUtc)
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken);

        var dueSoonCount = 0;
        var missedCount = 0;

        foreach (var assignment in candidates)
        {
            // Vadesi geçmişse "teslim edilmedi", geçmemişse "az kaldı" — ikisi aynı turda çakışmaz.
            if (assignment.MarkMissed(now))
            {
                missedCount++;
            }
            else if (assignment.MarkDueSoon(now))
            {
                dueSoonCount++;
            }
        }

        if (dueSoonCount + missedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AssignmentDueScanResult(dueSoonCount, missedCount);
    }
}

/// <summary>M06-3: Tarama turunu saatte bir koşturan arka plan servisi.</summary>
internal sealed class AssignmentDueScanner(
    IServiceScopeFactory scopeFactory,
    ILogger<AssignmentDueScanner> logger) : LeasedBackgroundService(scopeFactory, logger)
{
    protected override string JobName => "assignment-due-scan";

    protected override TimeSpan PollInterval => TimeSpan.FromHours(1);

    protected override ModuleDbContext ResolveDbContext(IServiceProvider services)
        => services.GetRequiredService<AssignmentsDbContext>();

    protected override async Task ExecuteCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var processor = services.GetRequiredService<IAssignmentDueScanProcessor>();
        var result = await processor.ScanAsync(cancellationToken);
        if (result.Total > 0)
        {
            logger.LogInformation(
                "Assignment due scan raised {DueSoonCount} due-soon and {MissedCount} missed warning(s).",
                result.DueSoonCount,
                result.MissedCount);
        }
    }
}
