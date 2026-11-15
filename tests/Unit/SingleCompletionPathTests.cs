using System.Text.Json;
using EgitimUssu.Shared.Application;
using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Unit;

public sealed class SingleCompletionPathTests
{
    private static readonly DateTime Start = new(2026, 11, 9, 14, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completion_sync_is_idempotent_and_preserves_rescheduled_time(bool recurring)
    {
        await using var services = Services();
        var db = services.GetRequiredService<SchedulingDbContext>();
        var lesson = NewLesson(recurring);
        db.LessonSchedules.Add(lesson);
        var key = recurring ? Start.AddDays(7) : Start;
        if (recurring) db.LessonOccurrenceExceptions.Add(new(Guid.NewGuid(), lesson.Id, key,
            OccurrenceExceptionAction.Rescheduled, key.AddHours(2), key.AddHours(3), "Erteleme", Start));
        await db.SaveChangesAsync();
        lesson.ClearDomainEvents();
        var session = Session(lesson, key);
        var handler = Handler(services, session);
        var envelope = Envelope(session);
        await handler.HandleAsync(envelope);
        await handler.HandleAsync(envelope);
        await handler.HandleAsync(Envelope(session));
        if (recurring)
        {
            var exception = await db.LessonOccurrenceExceptions.SingleAsync();
            Assert.Equal(OccurrenceExceptionAction.Completed, exception.Action);
            Assert.Equal(key.AddHours(2), exception.OverrideStartAtUtc);
            Assert.Equal(LessonScheduleStatus.Planned, lesson.Status);
            var list = await services.GetRequiredService<IQueryHandler<ListLessonSchedulesForTeacherQuery, Result<IReadOnlyCollection<LessonScheduleResponse>>>>()
                .Handle(new(lesson.TeacherUserId!.Value, Start, Start.AddDays(15)), default);
            Assert.Equal("Completed", Assert.Single(list.Value!, item => item.OccurrenceStartAtUtc == key).Status);
            Assert.Equal(2, list.Value!.Count(item => item.Status == "Planned"));
        }
        else Assert.Equal(LessonScheduleStatus.Completed, lesson.Status);
        Assert.Empty(lesson.DomainEvents);
        Assert.Empty(await db.OutboxMessages.Where(item => item.Type == "LessonSessionCompletedDomainEvent").ToArrayAsync());
    }

    [Theory]
    [InlineData(OccurrenceScope.Single, 7, true)]
    [InlineData(OccurrenceScope.ThisAndFuture, 0, true)]
    [InlineData(OccurrenceScope.All, 0, true)]
    [InlineData(OccurrenceScope.Single, 14, false)]
    [InlineData(OccurrenceScope.ThisAndFuture, 14, false)]
    public async Task Completed_occurrence_cannot_be_cancelled_or_rescheduled(OccurrenceScope scope, int day, bool blocked)
    {
        await using var services = Services();
        var db = services.GetRequiredService<SchedulingDbContext>();
        var lesson = NewLesson(true);
        db.LessonSchedules.Add(lesson);
        db.LessonOccurrenceExceptions.Add(new(Guid.NewGuid(), lesson.Id, Start.AddDays(7),
            OccurrenceExceptionAction.Completed, null, null, null, Start));
        await db.SaveChangesAsync();
        var cancel = await services.GetRequiredService<ICommandHandler<CancelLessonScheduleCommand, Result<LessonScheduleResponse>>>()
            .Handle(new(lesson.Id, CancellationReason.Other, false, null, scope, Start.AddDays(day)), default);
        Assert.Equal(blocked, cancel.IsFailure);
        if (blocked) Assert.Equal("scheduling.occurrence_completed", cancel.Error.Code);
        // Ayrı iş birimi: iptal denemesi ertelemenin girdisini değiştirmesin.
        db.ChangeTracker.Clear();
        if (!blocked) return;
        var move = await services.GetRequiredService<ICommandHandler<RescheduleLessonScheduleCommand, Result<LessonScheduleResponse>>>()
            .Handle(new(lesson.Id, Start.AddDays(day).AddHours(2), Start.AddDays(day).AddHours(3), null, scope, Start.AddDays(day)), default);
        Assert.True(move.IsFailure);
        Assert.Equal("scheduling.occurrence_completed", move.Error.Code);
        Assert.Equal(OccurrenceExceptionAction.Completed, (await db.LessonOccurrenceExceptions.SingleAsync()).Action);
    }

    [Fact]
    public async Task Updating_series_cannot_rewrite_completed_history()
    {
        await using var services = Services();
        var db = services.GetRequiredService<SchedulingDbContext>();
        var lesson = NewLesson(true);
        db.LessonSchedules.Add(lesson);
        db.LessonOccurrenceExceptions.Add(new(Guid.NewGuid(), lesson.Id, Start.AddDays(7),
            OccurrenceExceptionAction.Completed, null, null, null, Start));
        await db.SaveChangesAsync();
        var result = await services.GetRequiredService<ICommandHandler<UpdateLessonScheduleCommand, Result<LessonScheduleResponse>>>()
            .Handle(new(lesson.Id, "Matematik", ScheduledLessonFormat.Online, Start.AddHours(2), Start.AddHours(3),
                "Europe/Istanbul", lesson.RecurrenceRule, 15, null, null, null), default);
        Assert.True(result.IsFailure);
        Assert.Equal("scheduling.occurrence_completed", result.Error.Code);
        Assert.Equal(Start, lesson.StartAtUtc);
    }

    [Fact]
    public void Completed_rescheduled_occurrence_is_visible_in_its_new_window()
    {
        var key = Start.AddDays(7);
        var moved = key.AddDays(-8);
        var occurrences = RecurrenceExpander.Expand(Start, Start.AddHours(1), "FREQ=WEEKLY;COUNT=3",
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"), moved, moved.AddHours(1),
            new[] { new OccurrenceOverride(key, OccurrenceExceptionAction.Completed, moved, moved.AddHours(1)) }).ToArray();
        var occurrence = Assert.Single(occurrences);
        Assert.True(occurrence.IsCompleted);
        Assert.Equal(key, occurrence.RecurrenceIdUtc);
        Assert.Equal(moved, occurrence.StartAtUtc);
    }

    [Fact]
    public async Task Legacy_source_and_ad_hoc_session_do_not_change_scheduling()
    {
        await using var services = Services();
        var db = services.GetRequiredService<SchedulingDbContext>();
        var lesson = NewLesson(false);
        db.LessonSchedules.Add(lesson);
        await db.SaveChangesAsync();
        var session = Session(lesson, Start) with { LessonScheduleId = null, OccurrenceStartUtc = null };
        var handler = Handler(services, session);
        Assert.False(handler.CanHandle(Envelope(session) with { SourceModule = "Scheduling" }));
        await handler.HandleAsync(Envelope(session));
        Assert.Equal(LessonScheduleStatus.Planned, lesson.Status);
        Assert.Empty(await db.LessonOccurrenceExceptions.ToArrayAsync());
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FixedClock());
        services.AddSingleton<ILocalCalendar>(new LocalCalendar("Europe/Istanbul", new FixedClock()));
        services.AddSingleton<IIdGenerator>(new Ids());
        services.AddSingleton<IDomainEventMapper>(new NoOpDomainEventMapper());
        services.AddSingleton<ICancellationPolicyDirectory>(new TestDoubles.FakeCancellationPolicyDirectory());
        services.AddSingleton<IStudentDirectory>(new TestDoubles.FakeStudentDirectory());
        services.AddSchedulingModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Postgres"] = $"InMemory:{Guid.NewGuid()}" }).Build());
        return services.BuildServiceProvider();
    }
    private static LessonSchedule NewLesson(bool recurring) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Matematik", ScheduledLessonFormat.Online, Start, Start.AddHours(1), "Europe/Istanbul",
        recurring ? "FREQ=WEEKLY;COUNT=3" : null, LessonScheduleStatus.Planned, 15, null, null, null, Start.AddDays(-1));
    private static LessonSessionDetails Session(LessonSchedule lesson, DateTime key) => new(Guid.NewGuid(), lesson.Id,
        lesson.TeacherUserId!.Value, lesson.StudentId, true, Start.AddDays(8), "Kesirler", null, null,
        OccurrenceStartUtc: key, PlannedStartAtUtc: key.AddHours(2));
    private static LessonSessionCompletedSchedulingHandler Handler(ServiceProvider services, LessonSessionDetails session)
        => new(services.GetRequiredService<SchedulingDbContext>(), new FixedClock(), new Sessions(session),
            services.GetRequiredService<ITeacherScheduleLock>(), new Ids());
    private static IntegrationEvent Envelope(LessonSessionDetails session) => new(Guid.NewGuid(), Start.AddDays(8),
        "LessonSessionCompletedDomainEvent", "LessonSessions", JsonSerializer.Serialize(new { lessonSessionId = session.Id }));
    private sealed class Sessions(LessonSessionDetails session) : ILessonSessionAccessService
    {
        public Task<LessonSessionDetails?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<LessonSessionDetails?>(session);
    }
    private sealed class FixedClock : IClock { public DateTime UtcNow => Start.AddDays(8); }
    private sealed class Ids : IIdGenerator { public Guid New() => Guid.NewGuid(); }
}
