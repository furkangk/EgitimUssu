using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P08 Task 1 / M10-1: Gelişim anlık görüntüsü <b>gerçek Postgres'te</b>.
/// InMemory sağlayıcısı ne <c>GROUP BY</c> çevirisini ne de benzersiz indeksi zorlar; ikisi de
/// yalnız burada kanıtlanır (Öğrenilenler #81/#83: özel bir record'a projekte eden EF sorgusu
/// InMemory'de yeşil geçip üretimde patlayabilir).
/// </summary>
[Collection("containers")]
public sealed class RealProgressSnapshotTests(ContainerFixture fixture)
{
    private static readonly DateTime Now = new(2026, 9, 4, 6, 0, 0, DateTimeKind.Utc);

    [SkippableFact]
    public async Task Snapshot_Run_Should_Aggregate_On_Real_Postgres_And_Be_Idempotent()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var studentId = Guid.NewGuid();
        await SeedMasteryAsync(factory, studentId);

        int firstRun;
        int secondRun;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
            var processor = new ProgressSnapshotProcessor(dbContext, new FixedClock(Now), new GuidIdGenerator(), new LocalCalendar("Europe/Istanbul", new FixedClock(Now)));

            // GROUP BY + AVG/SUM çevirisi burada gerçekten SQL'e dönüşüyor mu?
            firstRun = await processor.RunAsync();
            secondRun = await processor.RunAsync();
        }

        Assert.Equal(2, firstRun);   // kapanmış hafta + kapanmış ay (tek ders)
        Assert.Equal(0, secondRun);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
            var weekly = await dbContext.ProgressSnapshots
                .SingleAsync(snapshot => snapshot.StudentId == studentId && snapshot.Period == SnapshotPeriod.Weekly);

            Assert.Equal("Matematik", weekly.Subject);
            Assert.Equal(new DateOnly(2026, 8, 24), weekly.PeriodStart);
            Assert.Equal(36, weekly.MasteryScore);   // (30.00 + 42.00) / 2
            Assert.Equal(180, weekly.StudyMinutes);
            Assert.Equal(1, weekly.TestCount);
            Assert.Equal(0.6m, weekly.AverageNet);
        }
    }

    [SkippableFact]
    public async Task Duplicate_Snapshot_Should_Be_Rejected_By_Unique_Index()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var studentId = Guid.NewGuid();
        var periodStart = new DateOnly(2026, 8, 24);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
            dbContext.ProgressSnapshots.Add(Snapshot(studentId, periodStart, 40));
            await dbContext.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
            dbContext.ProgressSnapshots.Add(Snapshot(studentId, periodStart, 90));

            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        }
    }

    [SkippableFact]
    public async Task Snapshot_Query_Should_Return_Series_With_Trend_On_Real_Postgres()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var studentId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
            dbContext.ProgressSnapshots.AddRange(
                Snapshot(studentId, new DateOnly(2026, 8, 17), 50),
                Snapshot(studentId, new DateOnly(2026, 8, 24), 58),
                Snapshot(studentId, new DateOnly(2026, 8, 31), 60),
                Snapshot(studentId, new DateOnly(2026, 9, 7), 52));
            await dbContext.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<
                IQueryHandler<ListProgressSnapshotsQuery, Result<IReadOnlyCollection<ProgressSnapshotResponse>>>>();

            var all = await handler.Handle(new ListProgressSnapshotsQuery(studentId, null, "Weekly", null, null), default);
            Assert.True(all.IsSuccess);
            Assert.NotNull(all.Value);
            Assert.Equal(
                ["Stable", "Improving", "Stable", "Declining"],
                all.Value.Select(item => item.Trend).ToArray());

            // from/to filtresi seriyi kırpar; kırpılan serinin ilk öğesinin trendi Stable olur.
            var window = await handler.Handle(
                new ListProgressSnapshotsQuery(studentId, "Matematik", "Weekly", new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 31)), default);
            Assert.True(window.IsSuccess);
            Assert.NotNull(window.Value);
            Assert.Equal(["Stable", "Stable"], window.Value.Select(item => item.Trend).ToArray());
        }
    }

    private static async Task SeedMasteryAsync(WebApplicationFactory<Program> factory, Guid studentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();

        var derivative = new TopicMastery(Guid.NewGuid(), studentId, "Matematik", "Türev", Now.AddDays(-10));
        derivative.RegisterStudy(180, Now.AddDays(-10));          // skor 30.00

        var integral = new TopicMastery(Guid.NewGuid(), studentId, "Matematik", "İntegral", Now.AddDays(-10));
        integral.RegisterTest(20, 12m, Now.AddDays(-10));         // skor 42.00, net oranı 0.6

        dbContext.TopicMasteries.AddRange(derivative, integral);
        await dbContext.SaveChangesAsync();
    }

    private static ProgressSnapshot Snapshot(Guid studentId, DateOnly periodStart, int masteryScore) =>
        new(Guid.NewGuid(), studentId, "Matematik", SnapshotPeriod.Weekly, periodStart, masteryScore, 120, 3, 0.5m, Now);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class GuidIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }
}
