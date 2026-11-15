using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Unit;

public sealed class StudyStreakEfTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);
    private sealed class Clock : IClock { public DateTime UtcNow { get; set; } = Now; }
    private sealed class User : ICurrentUser
    {
        public string? UserId => Guid.Empty.ToString();
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => ["Student"];
        public bool IsAuthenticated => true;
    }
    private static ServiceProvider Services(Clock clock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<ILocalCalendar>(new LocalCalendar("Europe/Istanbul", clock));
        services.AddSingleton<IIdGenerator>(new FakeIdGen());
        services.AddSingleton<ICurrentUser>(new User());
        services.AddSingleton<IDomainEventMapper>(new NoOpDomainEventMapper());
        services.AddStudyModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Postgres"] = $"InMemory:{Guid.NewGuid()}" }).Build());
        return services.BuildServiceProvider();
    }
    private static StudySession Manual(Guid student, DateTime end, int minutes = 90)
        => StudySession.CreateManual(Guid.NewGuid(), student, "Mat", null, minutes, end, null, false, false, Now);
    private static async Task Goal(StudyDbContext db, Guid student, int daily = 60)
    {
        db.StudyGoals.Add(new StudyGoal(Guid.NewGuid(), student, daily, null, null, null, null, 100, Now));
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task First_Unsaved_Manual_Or_Stopwatch_Session_Counts(bool stopwatch)
    {
        using var services = Services(new Clock());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
        var student = Guid.NewGuid();
        await Goal(db, student);
        var repo = scope.ServiceProvider.GetRequiredService<IStudyRepository>();
        if (stopwatch)
        {
            var session = StudySession.StartStopwatch(Guid.NewGuid(), student, "Mat", null, false, false, Now.AddMinutes(-90));
            db.StudySessions.Add(session);
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<EgitimUssu.Shared.Application.ICommandHandler<CompleteStudySessionCommand, Result<StudySessionResponse>>>()
                .Handle(new CompleteStudySessionCommand(session.Id, null, 90), default);
        }
        else
        {
            await scope.ServiceProvider.GetRequiredService<EgitimUssu.Shared.Application.ICommandHandler<CreateManualStudySessionCommand, Result<StudySessionResponse>>>()
                .Handle(new CreateManualStudySessionCommand(student, "Mat", null, 90, Now, null), default);
        }
        db.ChangeTracker.Clear();
        Assert.Equal(1, (await repo.GetStreakAsync(student, default))!.CurrentStreakDays);
    }

    [Theory]
    [InlineData(35, 25, 1)]
    [InlineData(35, 24, 0)]
    public async Task Night_Sessions_Sum_In_The_Previous_0400_Day(int previous, int current, int expected)
    {
        using var services = Services(new Clock());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
        var student = Guid.NewGuid();
        await Goal(db, student);
        db.StudySessions.Add(Manual(student, Now.AddDays(-1), previous));
        // 15 Kasım 01:30 TR: 14 Kasım seri günü, henüz kaydedilmemiş.
        var night = Manual(student, new DateTime(2026, 11, 14, 22, 30, 0, DateTimeKind.Utc), current);
        await db.SaveChangesAsync();
        db.StudySessions.Add(night);
        await scope.ServiceProvider.GetRequiredService<StudyCompletionService>().RecordCompletedAsync(night, default);
        var streak = await db.StudyStreaks.SingleAsync();
        Assert.Equal(expected, streak.CurrentStreakDays);
        Assert.Equal(expected == 1 ? new DateOnly(2026, 11, 14) : (DateOnly?)null, streak.LastStudiedOnDate);
    }

    [Theory]
    [InlineData(0, 59, 1)]
    [InlineData(1, 0, 0)]
    public async Task Exactly_0400_Starts_A_New_Threshold_Window(int hour, int minute, int expected)
    {
        using var services = Services(new Clock());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
        var student = Guid.NewGuid();
        await Goal(db, student);
        db.Add(Manual(student, new DateTime(2026, 11, 14, 20, 0, 0, DateTimeKind.Utc), 35));
        await db.SaveChangesAsync();
        var session = Manual(student, new DateTime(2026, 11, 15, hour, minute, 0, DateTimeKind.Utc), 25);
        db.Add(session);
        await scope.ServiceProvider.GetRequiredService<StudyCompletionService>().RecordCompletedAsync(session, default);
        Assert.Equal(expected, (await db.StudyStreaks.SingleAsync()).TotalStudyDays);
    }

    [Fact]
    public void Study_Day_Uses_Local_Wall_Clock_Through_Dst_And_Window_Start_Is_Inclusive()
    {
        var calendar = new LocalCalendar("Europe/Berlin", new Clock());
        var day = new DateOnly(2026, 3, 29); // DST başlangıcı; 04:30 yerel = 02:30Z.
        Assert.Equal(day, calendar.StudyDay(new DateTime(2026, 3, 29, 2, 30, 0, DateTimeKind.Utc)));
        var (start, end) = calendar.StudyDayRangeUtc(day);
        Assert.Equal(day, calendar.StudyDay(start));
        Assert.Equal(day.AddDays(-1), calendar.StudyDay(start.AddTicks(-1)));
        Assert.Equal(day.AddDays(1), calendar.StudyDay(end));
    }

    [Fact]
    public async Task Persisted_Current_Session_Is_Not_Double_Counted()
    {
        using var services = Services(new Clock());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
        var student = Guid.NewGuid();
        await Goal(db, student);
        var session = Manual(student, Now, 35);
        db.StudySessions.Add(session);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<StudyCompletionService>().RecordCompletedAsync(session, default);
        Assert.Equal(0, (await db.StudyStreaks.SingleAsync()).TotalStudyDays);
    }

    [Fact]
    public async Task Two_Backdated_Entries_Count_One_Day_Across_Contexts()
    {
        using var services = Services(new Clock());
        var student = Guid.NewGuid();
        foreach (var day in new[] { Now, Now.AddDays(-2), Now.AddDays(-2) })
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
            if (!await db.StudyGoals.AnyAsync()) await Goal(db, student);
            var session = Manual(student, day);
            db.Add(session);
            await scope.ServiceProvider.GetRequiredService<StudyCompletionService>().RecordCompletedAsync(session, default);
        }
        using var read = services.CreateScope();
        var streak = await read.ServiceProvider.GetRequiredService<StudyDbContext>().StudyStreaks.SingleAsync();
        Assert.Equal(2, streak.TotalStudyDays);
        Assert.Equal(1, streak.CurrentStreakDays);
        Assert.Equal(new DateOnly(2026, 11, 15), streak.LastStudiedOnDate);
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(1, 2)]
    public async Task Read_Broken_Streak_Is_Zero_And_0400_Today_Is_Used(int daysAgo, int expected)
    {
        // 15 Kasım 01:30 TR: hâlâ 14 Kasım seri günü.
        var clock = new Clock { UtcNow = new DateTime(2026, 11, 14, 22, 30, 0, DateTimeKind.Utc) };
        using var services = Services(clock);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
        var student = Guid.NewGuid();
        var streak = new StudyStreak(Guid.NewGuid(), student, Now);
        var last = new DateOnly(2026, 11, 15).AddDays(-daysAgo);
        streak.Recalculate([last.AddDays(-1), last], last, Now);
        db.Add(streak);
        await db.SaveChangesAsync();
        var result = await scope.ServiceProvider.GetRequiredService<EgitimUssu.Shared.Application.IQueryHandler<GetStreakQuery, Result<StreakResponse>>>()
            .Handle(new GetStreakQuery(student), default);
        Assert.Equal(expected, result.Value!.CurrentStreakDays);
        Assert.Equal(daysAgo == 1, result.Value!.StudiedToday);
    }

    [Fact]
    public async Task Repair_Twice_Is_Idempotent_And_Resets_Students_Without_Sessions()
    {
        using var services = Services(new Clock());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
        var student = Guid.NewGuid();
        await Goal(db, student);
        db.AddRange(Manual(student, Now), Manual(student, Now.AddDays(-1)), Manual(student, Now.AddDays(-1)));
        var empty = new StudyStreak(Guid.NewGuid(), Guid.NewGuid(), Now);
        empty.Recalculate([new DateOnly(2026, 11, 15)], new DateOnly(2026, 11, 15), Now);
        db.Add(empty);
        await db.SaveChangesAsync();
        var job = new StudyStreakRepairJob(db, scope.ServiceProvider.GetRequiredService<ILocalCalendar>(), new Clock(), new FakeIdGen());
        await job.RunAsync();
        db.ChangeTracker.Clear();
        var first = await db.StudyStreaks.SingleAsync(s => s.StudentId == student);
        Assert.Equal((2, 2, 2), (first.CurrentStreakDays, first.LongestStreakDays, first.TotalStudyDays));
        Assert.Equal(0, (await db.StudyStreaks.SingleAsync(s => s.StudentId == empty.StudentId)).TotalStudyDays);
        var before = (first.Id, first.UpdatedOnUtc, first.TotalStudyDays);
        await job.RunAsync();
        db.ChangeTracker.Clear();
        first = await db.StudyStreaks.SingleAsync(s => s.StudentId == student);
        Assert.Equal(before, (first.Id, first.UpdatedOnUtc, first.TotalStudyDays));
    }
}
