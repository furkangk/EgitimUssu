using System.Diagnostics;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Infrastructure.Observability;
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

    /// <summary>Her uyarı türünde işlenecek üst sınır (turda toplam en fazla 400).</summary>
    private const int BatchSize = 200;

    private readonly AssignmentsDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILocalCalendar _calendar;

    public AssignmentDueScanProcessor(AssignmentsDbContext dbContext, IClock clock, ILocalCalendar calendar)
    {
        _dbContext = dbContext;
        _clock = clock;
        _calendar = calendar;
    }

    public async Task<AssignmentDueScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        // C-04: Periyodik işin kendi span'ı. Olmazsa Npgsql bu turun her sorgusunu ayrı bir
        // **kök trace** olarak yayar (bkz. EgitimUssuActivitySource).
        using var activity = EgitimUssuActivitySource.Instance.StartActivity(
            "scan.assignments.due",
            ActivityKind.Internal);

        var now = _clock.UtcNow;
        // DueDateUtc bir tarih seçiminin UTC temsili; teslim sınırı o yerel günün sonudur.
        // Bitmiş günlerin üst sınırını takvimde bir kez çöz, SQL'de indeksli UTC aralığı kullan.
        var missedBefore = EndedDaysBoundary(now);
        var dueSoonBefore = EndedDaysBoundary(now + DueSoonWindow);
        var outstanding = _dbContext.Assignments.IgnoreAutoIncludes()
            .Where(assignment => assignment.SubmittedOnUtc == null
                && (assignment.Status == AssignmentStatus.Pending
                    || assignment.Status == AssignmentStatus.ReturnedForRevision));

        // İki kuyruğun ayrı bütçesi var; birikmiş kaçırmalar yaklaşan ödevleri aç bırakmaz.
        var dueSoon = await outstanding
            .Where(assignment => assignment.DueSoonNotifiedOnUtc == null
                && assignment.DueDateUtc >= missedBefore && assignment.DueDateUtc < dueSoonBefore)
            .OrderBy(assignment => assignment.DueDateUtc).ThenBy(assignment => assignment.Id)
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken);
        var missed = await outstanding
            .Where(assignment => assignment.MissedNotifiedOnUtc == null
                && assignment.DueDateUtc < missedBefore)
            .OrderBy(assignment => assignment.DueDateUtc).ThenBy(assignment => assignment.Id)
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken);

        var dueSoonCount = dueSoon.Count(assignment => assignment.MarkDueSoon(now, _calendar));
        var missedCount = missed.Count(assignment => assignment.MarkMissed(now, _calendar));

        if (dueSoonCount + missedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AssignmentDueScanResult(dueSoonCount, missedCount);
    }

    private DateTime EndedDaysBoundary(DateTime instant)
    {
        var range = _calendar.DayRangeUtc(_calendar.ToLocalDate(instant));
        return range.EndUtc.AddTicks(-10) <= instant ? range.EndUtc : range.StartUtc;
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
