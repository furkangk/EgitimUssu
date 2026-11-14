using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P08 Task 1 / M10-1 — haftalık-aylık gelişim anlık görüntüsü (<see cref="ProgressSnapshot"/>) ve trend.
/// Trend eşiği ±5 puan; hafta başlangıcı Pazartesi (UTC). Anlık görüntü üretimi aynı dönem için
/// tekrarlanmaz (benzersiz <c>(StudentId, Subject, Period, PeriodStart)</c>).
/// </summary>
public sealed class ProgressSnapshotTests
{
    // 2026-09-04 Cuma → içinde bulunulan haftanın Pazartesi'si 2026-08-31.
    private static readonly DateTime Now = new(2026, 9, 4, 6, 0, 0, DateTimeKind.Utc);

    // --- Trend -------------------------------------------------------------------------------

    [Fact]
    public void Trend_Should_Be_Improving_When_Score_Increases_By_Five_Or_More()
    {
        Assert.Equal(ProgressTrend.Improving, ProgressTrendCalculator.FromScores(60, 65));
        Assert.Equal(ProgressTrend.Improving, ProgressTrendCalculator.FromScores(60, 80));
    }

    [Fact]
    public void Trend_Should_Be_Stable_Within_Threshold()
    {
        Assert.Equal(ProgressTrend.Stable, ProgressTrendCalculator.FromScores(60, 64));
        Assert.Equal(ProgressTrend.Stable, ProgressTrendCalculator.FromScores(60, 56));
        Assert.Equal(ProgressTrend.Stable, ProgressTrendCalculator.FromScores(60, 60));

        // Serinin ilk öğesinin karşılaştıracağı önceki dönem yoktur.
        Assert.Equal(ProgressTrend.Stable, ProgressTrendCalculator.FromScores(null, 90));
    }

    [Fact]
    public void Trend_Should_Be_Declining_When_Score_Drops()
    {
        Assert.Equal(ProgressTrend.Declining, ProgressTrendCalculator.FromScores(60, 55));
        Assert.Equal(ProgressTrend.Declining, ProgressTrendCalculator.FromScores(60, 10));
    }

    // --- Dönem sınırları ---------------------------------------------------------------------

    [Fact]
    public void Week_Start_Should_Be_Monday_In_Utc()
    {
        var monday = new DateOnly(2026, 8, 31);

        Assert.Equal(monday, SnapshotPeriods.StartOf(SnapshotPeriod.Weekly, monday));
        Assert.Equal(monday, SnapshotPeriods.StartOf(SnapshotPeriod.Weekly, new DateOnly(2026, 9, 4)));  // Cuma
        Assert.Equal(monday, SnapshotPeriods.StartOf(SnapshotPeriod.Weekly, new DateOnly(2026, 9, 6)));  // Pazar
        Assert.Equal(new DateOnly(2026, 9, 7), SnapshotPeriods.StartOf(SnapshotPeriod.Weekly, new DateOnly(2026, 9, 7)));
    }

    [Fact]
    public void Month_Start_Should_Be_First_Day_And_Previous_Should_Step_One_Period_Back()
    {
        Assert.Equal(new DateOnly(2026, 9, 1), SnapshotPeriods.StartOf(SnapshotPeriod.Monthly, new DateOnly(2026, 9, 4)));
        Assert.Equal(new DateOnly(2026, 8, 1), SnapshotPeriods.Previous(SnapshotPeriod.Monthly, new DateOnly(2026, 9, 1)));
        Assert.Equal(new DateOnly(2026, 8, 24), SnapshotPeriods.Previous(SnapshotPeriod.Weekly, new DateOnly(2026, 8, 31)));
    }

    // --- Üretim turu -------------------------------------------------------------------------

    [Fact]
    public async Task Snapshot_Service_Should_Be_Idempotent_For_Same_Period()
    {
        await using var db = NewContext();
        var student = Guid.NewGuid();
        db.TopicMasteries.Add(Mastery(student, "Matematik", "Türev", studyMinutes: 180));
        await db.SaveChangesAsync();

        var processor = new ProgressSnapshotProcessor(db, new FixedClock(Now), new SeqIdGenerator(), new LocalCalendar("Europe/Istanbul", new FixedClock(Now)));

        var firstRun = await processor.RunAsync();
        var secondRun = await processor.RunAsync();

        Assert.Equal(2, firstRun);   // kapanmış hafta + kapanmış ay
        Assert.Equal(0, secondRun);
        Assert.Equal(1, await db.ProgressSnapshots.CountAsync(s => s.Period == SnapshotPeriod.Weekly));
        Assert.Equal(1, await db.ProgressSnapshots.CountAsync(s => s.Period == SnapshotPeriod.Monthly));
    }

    [Fact]
    public async Task Snapshot_Should_Cover_The_Last_Closed_Period()
    {
        await using var db = NewContext();
        var student = Guid.NewGuid();
        db.TopicMasteries.Add(Mastery(student, "Matematik", "Türev", studyMinutes: 180));
        await db.SaveChangesAsync();

        await new ProgressSnapshotProcessor(db, new FixedClock(Now), new SeqIdGenerator(), new LocalCalendar("Europe/Istanbul", new FixedClock(Now))).RunAsync();

        var weekly = await db.ProgressSnapshots.SingleAsync(s => s.Period == SnapshotPeriod.Weekly);
        var monthly = await db.ProgressSnapshots.SingleAsync(s => s.Period == SnapshotPeriod.Monthly);

        Assert.Equal(new DateOnly(2026, 8, 24), weekly.PeriodStart);   // 2026-08-31 haftası hâlâ açık
        Assert.Equal(new DateOnly(2026, 8, 1), monthly.PeriodStart);   // 2026-09 ayı hâlâ açık
        Assert.Equal(Now, weekly.CreatedOnUtc);
    }

    [Theory]
    // Pazartesi 16 Kasım 00:30 TR (Pazar 21:30Z): 9 Kasım haftası artık kapandı (UTC günü Pazar → 2 Kasım derdi).
    [InlineData("2026-11-15T21:30:00Z", SnapshotPeriod.Weekly, "2026-11-09")]
    // 1 Aralık 00:30 TR (30 Kasım 21:30Z): Kasım kapandı (UTC günü 30 Kasım → Ekim derdi).
    [InlineData("2026-11-30T21:30:00Z", SnapshotPeriod.Monthly, "2026-11-01")]
    public async Task Closed_Period_Is_Decided_On_The_Turkey_Local_Day(string nowUtc, SnapshotPeriod period, string expectedStart)
    {
        // P16 T5 / N-50 (K-19).
        var now = DateTime.Parse(nowUtc, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        await using var db = NewContext();
        db.TopicMasteries.Add(Mastery(Guid.NewGuid(), "Matematik", "Türev", studyMinutes: 180));
        await db.SaveChangesAsync();

        await new ProgressSnapshotProcessor(
            db, new FixedClock(now), new SeqIdGenerator(), new LocalCalendar("Europe/Istanbul", new FixedClock(now))).RunAsync();

        var snapshot = await db.ProgressSnapshots.SingleAsync(s => s.Period == period);
        Assert.Equal(DateOnly.Parse(expectedStart, System.Globalization.CultureInfo.InvariantCulture), snapshot.PeriodStart);
    }

    [Fact]
    public async Task Snapshot_Should_Aggregate_Topics_Of_The_Same_Subject()
    {
        await using var db = NewContext();
        var student = Guid.NewGuid();
        var other = Guid.NewGuid();
        db.TopicMasteries.AddRange(
            Mastery(student, "Matematik", "Türev", studyMinutes: 180),                              // skor 30.00
            Mastery(student, "Matematik", "İntegral", totalQuestions: 20, net: 12m),                // skor 42.00, net 0.6
            Mastery(student, "Fizik", "Optik", studyMinutes: 90),                                   // ayrı ders
            Mastery(other, "Matematik", "Türev", studyMinutes: 180));                               // ayrı öğrenci
        await db.SaveChangesAsync();

        await new ProgressSnapshotProcessor(db, new FixedClock(Now), new SeqIdGenerator(), new LocalCalendar("Europe/Istanbul", new FixedClock(Now))).RunAsync();

        var math = await db.ProgressSnapshots
            .SingleAsync(s => s.StudentId == student && s.Subject == "Matematik" && s.Period == SnapshotPeriod.Weekly);

        Assert.Equal(36, math.MasteryScore);        // (30 + 42) / 2
        Assert.Equal(180, math.StudyMinutes);
        Assert.Equal(1, math.TestCount);
        Assert.Equal(0.6m, math.AverageNet);
        Assert.Equal(2, await db.ProgressSnapshots.CountAsync(s => s.StudentId == student && s.Period == SnapshotPeriod.Weekly));
        Assert.Equal(1, await db.ProgressSnapshots.CountAsync(s => s.StudentId == other && s.Period == SnapshotPeriod.Weekly));
    }

    // --- Sorgu -------------------------------------------------------------------------------

    [Fact]
    public async Task Query_Should_Attach_Trend_Per_Subject_Series()
    {
        var student = Guid.NewGuid();
        var repository = new FakeSnapshotRepository(
        [
            Snapshot(student, "Matematik", new DateOnly(2026, 8, 17), 50),
            Snapshot(student, "Matematik", new DateOnly(2026, 8, 24), 58),   // +8  → Improving
            Snapshot(student, "Matematik", new DateOnly(2026, 8, 31), 60),   // +2  → Stable
            Snapshot(student, "Matematik", new DateOnly(2026, 9, 7), 52),    // -8  → Declining
            Snapshot(student, "Fizik", new DateOnly(2026, 8, 31), 90)        // serinin ilki → Stable
        ]);

        var result = await new ListProgressSnapshotsQueryHandler(repository)
            .Handle(new ListProgressSnapshotsQuery(student, null, null, null, null), default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var items = result.Value.ToArray();

        // Seriler ders adına göre sıralanır: önce Fizik (tek öğe), sonra Matematik zaman serisi.
        Assert.Equal(
            ["Stable", "Stable", "Improving", "Stable", "Declining"],
            items.Select(item => item.Trend).ToArray());
        Assert.Equal("Fizik", items[0].Subject);
        Assert.Equal("Weekly", items[0].Period);
        Assert.Equal(
            [new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 7)],
            items.Skip(1).Select(item => item.PeriodStart).ToArray());
    }

    // --- Yardımcılar -------------------------------------------------------------------------

    private static ProgressTrackingDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ProgressTrackingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new JsonDomainEventMapper());

    private static TopicMastery Mastery(
        Guid studentId,
        string subject,
        string topic,
        int studyMinutes = 0,
        int totalQuestions = 0,
        decimal net = 0m)
    {
        var mastery = new TopicMastery(Guid.NewGuid(), studentId, subject, topic, Now.AddDays(-10));
        if (studyMinutes > 0)
        {
            mastery.RegisterStudy(studyMinutes, Now.AddDays(-10));
        }

        if (totalQuestions > 0)
        {
            mastery.RegisterTest(totalQuestions, net, Now.AddDays(-10));
        }

        mastery.ClearDomainEvents();
        return mastery;
    }

    private static ProgressSnapshot Snapshot(Guid studentId, string subject, DateOnly periodStart, int masteryScore) =>
        new(Guid.NewGuid(), studentId, subject, SnapshotPeriod.Weekly, periodStart, masteryScore, 120, 3, 0.5m, Now);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class SeqIdGenerator : IIdGenerator
    {
        private int _next;

        public Guid New() => new(++_next, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);
    }

    /// <summary>Yalnız anlık görüntü listelemesini karşılayan sahte depo (sorgu handler'ı için).</summary>
    private sealed class FakeSnapshotRepository(IReadOnlyList<ProgressSnapshot> snapshots) : IProgressRepository
    {
        public Task<IReadOnlyList<ProgressSnapshot>> ListSnapshotsAsync(
            Guid studentId, string? subject, SnapshotPeriod? period, DateOnly? from, DateOnly? to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProgressSnapshot>>(snapshots
                .Where(s => s.StudentId == studentId)
                .Where(s => subject is null || s.Subject == subject)
                .Where(s => period is null || s.Period == period)
                .Where(s => from is null || s.PeriodStart >= from)
                .Where(s => to is null || s.PeriodStart <= to)
                .ToArray());

        public Task<TopicMastery?> GetMasteryAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TopicMastery>> ListMasteryAsync(Guid studentId, string? subject, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddMasteryAsync(TopicMastery mastery, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TopicGoal?> GetGoalAsync(Guid goalId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TopicGoal>> ListGoalsAsync(Guid studentId, TopicGoalStatus? status, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TopicGoal>> ListActiveGoalsForTopicAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddGoalAsync(TopicGoal goal, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
