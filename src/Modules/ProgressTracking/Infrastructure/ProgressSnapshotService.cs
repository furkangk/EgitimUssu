using System.Diagnostics;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Shared.Infrastructure.Observability;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

/// <summary>M10-1: Anlık görüntü üretim turu (arka plan servisinden ayrık, test edilebilir).</summary>
public interface IProgressSnapshotProcessor
{
    /// <summary>Bir üretim turu koşturur.</summary>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Bu turda yazılan yeni anlık görüntü sayısı.</returns>
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// M10-1: <b>Kapanmış</b> son hafta ve son ay için eksik <see cref="ProgressSnapshot"/> satırlarını üretir.
/// Kaynak yalnız modül içi <see cref="TopicMastery"/>'dir (çapraz-modül DB okuması yok); ders bazında
/// konular toplanır: skor ortalama, süre/test sayısı toplam, net ortalama.
/// <para>
/// Idempotency iki katmanlıdır: tur, dönem için zaten yazılmış satırları atlar; veritabanında
/// <c>(StudentId, Subject, Period, PeriodStart)</c> benzersiz indeksi ikinci yazımı imkânsız kılar.
/// </para>
/// <para>
/// <b>Bilinçli sınır:</b> tur yalnız <i>en son kapanmış</i> dönemi üretir, daha eskileri geri doldurmaz.
/// <see cref="TopicMastery"/> kümülatif olduğu için geçmiş bir dönemin o günkü değeri yeniden
/// kurulamaz; geriye doldurmak düz (sahte) bir trend üretirdi. Servis günde bir koştuğu için
/// kapanmış dönem en geç bir gün içinde yakalanır.
/// </para>
/// </summary>
public sealed class ProgressSnapshotProcessor : IProgressSnapshotProcessor
{
    private static readonly SnapshotPeriod[] Periods = [SnapshotPeriod.Weekly, SnapshotPeriod.Monthly];

    private readonly ProgressTrackingDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;

    public ProgressSnapshotProcessor(ProgressTrackingDbContext dbContext, IClock clock, IIdGenerator idGenerator)
    {
        _dbContext = dbContext;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        // C-04: Periyodik işin kendi span'ı. Olmazsa Npgsql bu turun her sorgusunu ayrı bir
        // **kök trace** olarak yayar (bkz. EgitimUssuActivitySource).
        using var activity = EgitimUssuActivitySource.Instance.StartActivity(
            "scan.progress.snapshot",
            ActivityKind.Internal);

        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var createdCount = 0;

        foreach (var period in Periods)
        {
            var closedPeriodStart = SnapshotPeriods.Previous(period, SnapshotPeriods.StartOf(period, today));
            createdCount += await CreateMissingAsync(period, closedPeriodStart, now, cancellationToken);
        }

        if (createdCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return createdCount;
    }

    private async Task<int> CreateMissingAsync(
        SnapshotPeriod period,
        DateOnly periodStart,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var alreadyWritten = (await _dbContext.ProgressSnapshots
                .Where(snapshot => snapshot.Period == period && snapshot.PeriodStart == periodStart)
                .Select(snapshot => new { snapshot.StudentId, snapshot.Subject })
                .ToArrayAsync(cancellationToken))
            .Select(row => (row.StudentId, row.Subject))
            .ToHashSet();

        var subjectTotals = await _dbContext.TopicMasteries
            .GroupBy(mastery => new { mastery.StudentId, mastery.Subject })
            .Select(group => new SubjectTotals(
                group.Key.StudentId,
                group.Key.Subject,
                group.Average(mastery => mastery.MasteryScore),
                group.Sum(mastery => mastery.TotalStudyMinutes),
                group.Sum(mastery => mastery.TestAttemptCount),
                group.Average(mastery => mastery.AverageNetRatio)))
            .ToArrayAsync(cancellationToken);

        var createdCount = 0;
        foreach (var totals in subjectTotals)
        {
            if (alreadyWritten.Contains((totals.StudentId, totals.Subject)))
            {
                continue;
            }

            _dbContext.ProgressSnapshots.Add(new ProgressSnapshot(
                _idGenerator.New(),
                totals.StudentId,
                totals.Subject,
                period,
                periodStart,
                (int)Math.Round(totals.AverageMasteryScore, MidpointRounding.AwayFromZero),
                totals.StudyMinutes,
                totals.TestCount,
                totals.AverageNetRatio ?? 0m,
                now));
            createdCount++;
        }

        return createdCount;
    }

    /// <summary>Bir öğrencinin bir dersindeki tüm konularının toplamı (EF tarafında gruplanır).</summary>
    private sealed record SubjectTotals(
        Guid StudentId,
        string Subject,
        decimal AverageMasteryScore,
        int StudyMinutes,
        int TestCount,
        decimal? AverageNetRatio);
}

/// <summary>
/// M10-1: Üretim turunu günde bir koşturan arka plan servisi. Açılıştan hemen sonra koşmaz
/// (<see cref="StartupDelay"/>), böylece uygulama başlangıcı ve testler tura yakalanmaz.
/// </summary>
internal sealed class ProgressSnapshotService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProgressSnapshotService> _logger;

    public ProgressSnapshotService(IServiceScopeFactory scopeFactory, ILogger<ProgressSnapshotService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IProgressSnapshotProcessor>();
                var createdCount = await processor.RunAsync(stoppingToken);
                if (createdCount > 0)
                {
                    _logger.LogInformation("Progress snapshot cycle wrote {CreatedCount} snapshot(s).", createdCount);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Progress snapshot cycle failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
