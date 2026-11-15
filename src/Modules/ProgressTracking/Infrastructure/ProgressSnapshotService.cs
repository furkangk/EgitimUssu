using System.Diagnostics;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Shared.Infrastructure.Observability;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;

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

    private readonly ILocalCalendar _calendar;

    public ProgressSnapshotProcessor(ProgressTrackingDbContext dbContext, IClock clock, IIdGenerator idGenerator, ILocalCalendar calendar)
    {
        _dbContext = dbContext;
        _clock = clock;
        _idGenerator = idGenerator;
        _calendar = calendar;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        // C-04: Periyodik işin kendi span'ı. Olmazsa Npgsql bu turun her sorgusunu ayrı bir
        // **kök trace** olarak yayar (bkz. EgitimUssuActivitySource).
        using var activity = EgitimUssuActivitySource.Instance.StartActivity(
            "scan.progress.snapshot",
            ActivityKind.Internal);

        var now = _clock.UtcNow;
        var today = _calendar.ToLocalDate(now);   // P16 T5 / N-50: kapanmış hafta/ay Türkiye gününe göre
        // Günlük mevcut kira altında; snapshot sayısı sözleşmesi değişmez.
        while (true)
        {
            var expired = await _dbContext.TopicGoals.AsNoTracking().Where(g => g.Status == TopicGoalStatus.Active
                && g.TargetDate != null && g.TargetDate < today).OrderBy(g => g.TargetDate).ThenBy(g => g.Id)
                .Select(g => g.Id).Take(200).ToArrayAsync(cancellationToken);
            if (expired.Length == 0) break;
            var active = _dbContext.TopicGoals.Where(g => expired.Contains(g.Id) && g.Status == TopicGoalStatus.Active
                && g.TargetDate != null && g.TargetDate < today);
            if (_dbContext.Database.IsRelational())
                await active.ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, TopicGoalStatus.Missed), cancellationToken);
            else
            {
                var goals = await active.ToArrayAsync(cancellationToken);
                foreach (var goal in goals) goal.MarkMissed(today);
                await _dbContext.SaveChangesAsync(cancellationToken);
                foreach (var goal in goals) _dbContext.Entry(goal).State = EntityState.Detached;
            }
        }
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
        var createdCount = 0;
        Guid? afterStudent = null;
        while (true)
        {
            var studentsQuery = _dbContext.TopicMasteries.Select(x => x.StudentId).Distinct();
            if (afterStudent is { } after) studentsQuery = studentsQuery.Where(id => id.CompareTo(after) > 0);
            var students = await studentsQuery.OrderBy(id => id).Take(200).ToArrayAsync(cancellationToken);
            if (students.Length == 0) break;

            var alreadyWritten = (await _dbContext.ProgressSnapshots.AsNoTracking()
                    .Where(snapshot => students.Contains(snapshot.StudentId)
                        && snapshot.Period == period && snapshot.PeriodStart == periodStart)
                    .Select(snapshot => new { snapshot.StudentId, snapshot.Subject })
                    .ToArrayAsync(cancellationToken))
                .Select(row => (row.StudentId, row.Subject)).ToHashSet();
            var subjectTotals = await _dbContext.TopicMasteries.Where(x => students.Contains(x.StudentId))
                .GroupBy(mastery => new { mastery.StudentId, mastery.Subject })
                .Select(group => new SubjectTotals(group.Key.StudentId, group.Key.Subject,
                    group.Average(mastery => mastery.MasteryScore), group.Sum(mastery => mastery.TotalStudyMinutes),
                    group.Sum(mastery => mastery.TestAttemptCount), group.Average(mastery => mastery.AverageNetRatio)))
                .ToArrayAsync(cancellationToken);
            var added = new List<ProgressSnapshot>();
            foreach (var totals in subjectTotals)
            {
                if (alreadyWritten.Contains((totals.StudentId, totals.Subject))) continue;
                var snapshot = new ProgressSnapshot(_idGenerator.New(), totals.StudentId, totals.Subject, period, periodStart,
                    (int)Math.Round(totals.AverageMasteryScore, MidpointRounding.AwayFromZero), totals.StudyMinutes,
                    totals.TestCount, totals.AverageNetRatio ?? 0m, now);
                _dbContext.ProgressSnapshots.Add(snapshot);
                added.Add(snapshot);
            }
            if (added.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                foreach (var snapshot in added) _dbContext.Entry(snapshot).State = EntityState.Detached;
                createdCount += added.Count;
            }
            afterStudent = students[^1];
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
/// <summary>A-07: Tur başına kira alır; ikinci bir uygulama örneği aynı günün anlık görüntüsünü tekrar yazamaz.</summary>
internal sealed class ProgressSnapshotService(
    IServiceScopeFactory scopeFactory,
    ILogger<ProgressSnapshotService> logger) : LeasedBackgroundService(scopeFactory, logger)
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

    protected override string JobName => "progress-snapshot";

    protected override TimeSpan PollInterval => TimeSpan.FromHours(24);

    protected override ModuleDbContext ResolveDbContext(IServiceProvider services)
        => services.GetRequiredService<ProgressTrackingDbContext>();

    protected override async Task ExecuteCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var processor = services.GetRequiredService<IProgressSnapshotProcessor>();
        var createdCount = await processor.RunAsync(cancellationToken);
        if (createdCount > 0)
        {
            logger.LogInformation("Progress snapshot cycle wrote {CreatedCount} snapshot(s).", createdCount);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Açılışta hemen koşmaz: uygulama daha ısınırken günlük ağır taramayı başlatmanın anlamı yok.
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await base.ExecuteAsync(stoppingToken);
    }
}
