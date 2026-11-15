using System.Text.Json;
using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

public sealed class TeacherDashboardOccurrenceTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 21, 30, 0, DateTimeKind.Utc);
    private sealed class Clock : IClock { public DateTime UtcNow { get; set; } = Now; }
    private sealed class Source : ITeacherDashboardLessonSource
    {
        public List<DashboardLessonOccurrence> Rows { get; } = [];
        public Task<IReadOnlyCollection<DashboardLessonOccurrence>> ListDashboardOccurrencesAsync(
            DateTime fromUtc, DateTime toUtc, Guid? lessonScheduleId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<DashboardLessonOccurrence>>(Rows.Where(r =>
                (!lessonScheduleId.HasValue || r.LessonScheduleId == lessonScheduleId) && r.StartAtUtc >= fromUtc && r.StartAtUtc < toUtc).ToArray());
    }
    private static ReportingDbContext Db() => new(new DbContextOptionsBuilder<ReportingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new NoOpDomainEventMapper());
    private static LocalCalendar Calendar(Clock clock) => new("Europe/Istanbul", clock);
    private static async Task<TeacherDashboardProjection> Load(ReportingDbContext db, Guid teacher)
        => await db.TeacherDashboardProjections.Include(p => p.Items).SingleAsync(p => p.TeacherUserId == teacher);

    [Fact]
    public async Task Weekly_Occurrences_Roll_Into_Window_Without_New_Events()
    {
        await using var db = Db(); var source = new Source(); var clock = new Clock();
        var teacher = Guid.NewGuid(); var series = Guid.NewGuid();
        for (var week = 0; week < 5; week++)
        {
            var start = Now.AddDays(week * 7);
            source.Rows.Add(new(teacher, series, start, start, false));
        }
        var processor = new TeacherDashboardOccurrenceProcessor(db, source, Calendar(clock), clock);
        await processor.RunAsync();
        Assert.Equal(3, (await Load(db, teacher)).Items.Count(i => i.Kind == TeacherDashboardItemKind.Lesson));
        clock.UtcNow = Now.AddDays(21);
        await processor.RunAsync();
        var projection = await Load(db, teacher);
        Assert.Equal(2, projection.Items.Count(i => i.Kind == TeacherDashboardItemKind.Lesson));
        var range = Calendar(clock).DayRangeUtc(Calendar(clock).Today());
        Assert.Equal(1, projection.CountLessonsBetween(range.StartUtc, range.EndUtc));
    }

    [Fact]
    public void Tombstone_Cannot_Be_Overwritten_By_A_Scheduled_Occurrence()
    {
        var projection = new TeacherDashboardProjection(Guid.NewGuid()); var series = Guid.NewGuid();
        projection.TrackLessonOccurrence(series, Now, Now, true);
        projection.TrackLessonOccurrence(series, Now, Now, false);
        Assert.Equal(0, projection.CountLessonsBetween(Now.AddHours(-1), Now.AddHours(1)));
        Assert.Single(projection.Items);
    }

    [Fact]
    public async Task Rescheduling_Keeps_Original_Key_And_Cancels_Missing_Occurrences()
    {
        await using var db = Db(); var source = new Source(); var clock = new Clock();
        var teacher = Guid.NewGuid(); var series = Guid.NewGuid(); var other = Guid.NewGuid();
        source.Rows.Add(new(teacher, series, Now, Now, false));
        source.Rows.Add(new(teacher, other, Now, Now, false));
        var processor = new TeacherDashboardOccurrenceProcessor(db, source, Calendar(clock), clock);
        await processor.RunAsync();
        source.Rows[0] = source.Rows[0] with { StartAtUtc = Now.AddDays(1) };
        await processor.RunAsync();
        var projection = await Load(db, teacher);
        Assert.Equal(Now.AddDays(1), projection.Items.Single(i => i.ItemId == series).OccursAtUtc);
        source.Rows.RemoveAt(0);
        await processor.RunAsync();
        Assert.Equal(1, projection.CountLessonsBetween(Now.AddDays(-1), Now.AddDays(2)));
        Assert.Equal(2, projection.Items.Count);
    }

    [Fact]
    public void Pruning_Removes_Old_Lessons_Only_And_Uses_Visible_Time()
    {
        var projection = new TeacherDashboardProjection(Guid.NewGuid());
        projection.TrackLesson(Guid.NewGuid(), Now.AddDays(-30));
        projection.TrackLessonOccurrence(Guid.NewGuid(), Now.AddDays(-30), Now, false);
        Assert.Equal(2, projection.Items.Count);
        projection.TrackOpenAssignment(Guid.NewGuid()); projection.TrackStudent(Guid.NewGuid());
        projection.TrackOverduePayment(Guid.NewGuid(), 10, "TRY");
        projection.PruneLessons(Now.AddDays(-1), Now.AddDays(15));
        Assert.Equal(4, projection.Items.Count);
    }

    [Fact]
    public async Task Window_Includes_Yesterday_And_Full_Day_Plus14_Excludes_Plus15()
    {
        await using var db = Db(); var source = new Source(); var clock = new Clock();
        var calendar = Calendar(clock); var teacher = Guid.NewGuid();
        var start = calendar.DayRangeUtc(calendar.Today().AddDays(-1)).StartUtc;
        var end = calendar.DayRangeUtc(calendar.Today().AddDays(15)).StartUtc;
        foreach(var time in new[] { start.AddTicks(-1), start, end.AddTicks(-1), end })
            source.Rows.Add(new(teacher, Guid.NewGuid(), time, time, false));
        await new TeacherDashboardOccurrenceProcessor(db, source, calendar, clock).RunAsync();
        Assert.Equal(2, (await Load(db, teacher)).Items.Count);
    }
    [Fact]
    public async Task Cancellation_First_Creates_Tombstone_And_Replayed_Schedule_Cannot_Resurrect_It()
    {
        await using var db = Db(); var source = new Source(); var clock = new Clock();
        var teacher = Guid.NewGuid(); var series = Guid.NewGuid();
        source.Rows.Add(new(teacher, series, Now, Now, true));
        source.Rows.Add(new(teacher, series, Now.AddDays(7), Now.AddDays(7), false));
        var processor = new TeacherDashboardOccurrenceProcessor(db, source, Calendar(clock), clock);
        var payload = JsonSerializer.Serialize(new { lessonScheduleId = series, teacherUserId = teacher, startAtUtc = Now }, IntegrationEventSerialization.Options);
        var cancelled = new IntegrationEvent(Guid.NewGuid(), Now, "LessonCancellationAppliedDomainEvent", "Scheduling", payload);
        await new LessonScheduleCancelledProjectionHandler(db, clock, processor).HandleAsync(cancelled);
        var scheduled = new IntegrationEvent(Guid.NewGuid(), Now, "LessonScheduledDomainEvent", "Scheduling", payload);
        var handler = new LessonScheduleUpsertProjectionHandler(db, clock, processor);
        await handler.HandleAsync(scheduled);
        await handler.HandleAsync(scheduled);
        var projection = await Load(db, teacher);
        Assert.Equal(2, projection.Items.Count);
        Assert.Equal(0, projection.CountLessonsBetween(Now.AddHours(-1), Now.AddHours(1)));
        Assert.Equal(1, projection.CountLessonsBetween(Now.AddDays(6), Now.AddDays(8)));
        Assert.Equal(2, await db.InboxMessages.CountAsync());
        Assert.Equal(scheduled.EventId, projection.LastAppliedEventId);
    }

    [Fact]
    public async Task Query_Uses_Local_Day_And_HalfOpen_Midnight_Boundaries()
    {
        await using var db = Db(); var source = new Source(); var clock = new Clock();
        var teacher = Guid.NewGuid(); var calendar = Calendar(clock);
        var range = calendar.DayRangeUtc(calendar.Today());
        foreach (var time in new[] { range.StartUtc.AddTicks(-1), range.StartUtc, Now, range.EndUtc.AddTicks(-1), range.EndUtc })
            source.Rows.Add(new(teacher, Guid.NewGuid(), time, time, false));
        await new TeacherDashboardOccurrenceProcessor(db, source, calendar, clock).RunAsync();
        var result = await new GetTeacherDashboardQueryHandler(new TeacherDashboardProjectionReader(db), calendar)
            .Handle(new GetTeacherDashboardQuery(teacher), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.TodayLessonCount);
    }

    [Fact]
    public async Task Source_Can_Move_An_Occurrence_Out_Of_Window_And_Back_With_The_Same_Key()
    {
        await using var db = Db(); var source = new Source(); var clock = new Clock();
        var teacher = Guid.NewGuid(); var series = Guid.NewGuid();
        source.Rows.Add(new(teacher, series, Now, Now, false));
        var processor = new TeacherDashboardOccurrenceProcessor(db, source, Calendar(clock), clock);
        await processor.RunAsync();
        source.Rows[0] = source.Rows[0] with { StartAtUtc = Now.AddDays(40) };
        await processor.RunAsync();
        Assert.Equal(0, (await Load(db, teacher)).CountLessonsBetween(Now.AddDays(-1), Now.AddDays(2)));
        source.Rows[0] = source.Rows[0] with { StartAtUtc = Now.AddHours(1) };
        await processor.RunAsync();
        Assert.Equal(1, (await Load(db, teacher)).CountLessonsBetween(Now, Now.AddHours(2)));
        Assert.Single((await Load(db, teacher)).Items);
    }

    [Fact]
    public async Task Self_Schedule_Does_Not_Create_A_Teacher_Projection()
    {
        await using var db = Db(); var clock = new Clock(); var source = new Source();
        var processor = new TeacherDashboardOccurrenceProcessor(db, source, Calendar(clock), clock);
        var payload = JsonSerializer.Serialize(new { lessonScheduleId = Guid.NewGuid(), teacherUserId = (Guid?)null, startAtUtc = Now });
        await new LessonScheduleUpsertProjectionHandler(db, clock, processor).HandleAsync(
            new IntegrationEvent(Guid.NewGuid(), Now, "LessonScheduledDomainEvent", "Scheduling", payload));
        Assert.Empty(await db.TeacherDashboardProjections.ToArrayAsync());
    }

}
