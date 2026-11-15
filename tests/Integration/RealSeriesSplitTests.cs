using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P17 T2 — gerçek PostgreSQL'de seri bölme tek transaction'da eski seriyi sınırlar, yeni seriyi ekler, gelecekteki
/// istisnayı yeni anahtara taşır ve <see cref="LessonSeriesSplitDomainEvent"/> olayını outbox'a yazar.
/// </summary>
[Collection("containers")]
public sealed class RealSeriesSplitTests(ContainerFixture fixture)
{
    [SkippableTheory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public async Task This_and_future_split_persists_two_series_moved_exception_and_outbox_event_atomically(bool useUntil, int dayShift)
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();

        var anchor = NextMondayAt14Utc();
        var cutoff = anchor.AddDays(21);
        var newStart = cutoff.AddDays(dayShift).AddHours(2);
        var rule = useUntil ? $"FREQ=WEEKLY;BYDAY=MO;UNTIL={anchor.AddDays(63):yyyyMMddTHHmmssZ}" : "FREQ=WEEKLY;BYDAY=MO;COUNT=10";
        var oldId = Guid.NewGuid();
        var series = new LessonSchedule(
            oldId, Guid.NewGuid(), Guid.NewGuid(), "Matematik", ScheduledLessonFormat.Online,
            anchor, anchor.AddHours(1), "Europe/Istanbul", rule,
            LessonScheduleStatus.Planned, 60, "Zoom", null, "Gerçek seri", DateTime.UtcNow);
        series.ClearDomainEvents();
        var cancelled = new LessonOccurrenceException(
            Guid.NewGuid(), oldId, anchor.AddDays(35), OccurrenceExceptionAction.Cancelled,
            null, null, "Altıncı hafta iptal", DateTime.UtcNow);
        await db.LessonSchedules.AddAsync(series);
        await db.LessonOccurrenceExceptions.AddAsync(cancelled);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var handler = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<RescheduleLessonScheduleCommand, Result<LessonScheduleResponse>>>();
        var result = await handler.Handle(new RescheduleLessonScheduleCommand(
            oldId, newStart, newStart.AddHours(1), "Saat değişti",
            OccurrenceScope.ThisAndFuture, cutoff), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        var newId = result.Value!.Id;
        Assert.NotEqual(oldId, newId);

        db.ChangeTracker.Clear();
        var rows = await db.LessonSchedules.AsNoTracking()
            .Where(item => item.Id == oldId || item.Id == newId)
            .OrderBy(item => item.StartAtUtc)
            .ToArrayAsync();
        Assert.Equal(2, rows.Length);
        Assert.Equal(anchor, rows[0].StartAtUtc);
        Assert.Contains("UNTIL=", rows[0].RecurrenceRule);
        Assert.Equal(newStart, rows[1].StartAtUtc);
        if (!useUntil) Assert.Contains("COUNT=7", rows[1].RecurrenceRule);
        var future = RecurrenceExpander.Expand(rows[1].StartAtUtc, rows[1].EndAtUtc, rows[1].RecurrenceRule,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"), anchor, anchor.AddDays(90));
        Assert.Equal(Enumerable.Range(0, 7).Select(n => newStart.AddDays(n * 7)), future.Select(o => o.StartAtUtc));

        var moved = await db.LessonOccurrenceExceptions.AsNoTracking().SingleAsync(item => item.Id == cancelled.Id);
        Assert.Equal(newId, moved.SeriesLessonScheduleId);
        Assert.Equal(anchor.AddDays(35 + dayShift).AddHours(2), moved.OriginalStartAtUtc);
        Assert.Equal(OccurrenceExceptionAction.Cancelled, moved.Action);

        var splitOutbox = await db.OutboxMessages.AsNoTracking()
            .Where(item => item.Type == nameof(LessonSeriesSplitDomainEvent))
            .Select(item => item.Payload)
            .ToArrayAsync();
        Assert.Contains(splitOutbox, payload => payload.Contains(oldId.ToString(), StringComparison.OrdinalIgnoreCase)
            && payload.Contains(newId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static DateTime NextMondayAt14Utc()
    {
        var day = DateTime.UtcNow.Date.AddDays(14);
        while (day.DayOfWeek != DayOfWeek.Monday)
        {
            day = day.AddDays(1);
        }

        return DateTime.SpecifyKind(day.AddHours(14), DateTimeKind.Utc);
    }
}
