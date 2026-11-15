using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P17 T2 (K-20): <c>ThisAndFuture</c> bütün seriyi taşımak yerine geçmişi eski seride bırakıp yeni bir seri açar.
/// On oluşumlu haftalık seri dördüncü oluşumdan bölündüğünde 3 + 7 olarak kalır; gelecekteki istisnalar yeni serinin
/// zaman farkına taşınır ve iptal eylemi korunur.
/// </summary>
public sealed class SeriesSplitTests
{
    private static readonly DateTime Anchor = new(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Cutoff = Anchor.AddDays(21);
    private static readonly DateTime NewStart = Cutoff.AddHours(2);
    private static readonly FixedClock Clock = new(Anchor.AddDays(-7));
    private static readonly LocalCalendar Calendar = new("Europe/Istanbul", Clock);

    [Fact]
    public async Task This_and_future_reschedule_splits_counted_series_and_moves_future_exceptions()
    {
        var series = NewSeries();
        series.ClearDomainEvents();
        var store = new Store(series);

        var pastException = new LessonOccurrenceException(
            Guid.NewGuid(), series.Id, Anchor.AddDays(7), OccurrenceExceptionAction.Rescheduled,
            Anchor.AddDays(7).AddHours(1), Anchor.AddDays(7).AddHours(2), "Geçmiş", Clock.UtcNow);
        var cancelled = new LessonOccurrenceException(
            Guid.NewGuid(), series.Id, Anchor.AddDays(35), OccurrenceExceptionAction.Cancelled,
            null, null, "İptal", Clock.UtcNow);
        var moved = new LessonOccurrenceException(
            Guid.NewGuid(), series.Id, Anchor.AddDays(49), OccurrenceExceptionAction.Rescheduled,
            Anchor.AddDays(50), Anchor.AddDays(50).AddHours(1), "Sonraki güne", Clock.UtcNow);
        store.Exceptions.AddRange([pastException, cancelled, moved]);

        var result = await Handler(store).Handle(new RescheduleLessonScheduleCommand(
            series.Id, NewStart, NewStart.AddHours(1), "Saat değişti",
            OccurrenceScope.ThisAndFuture, Cutoff), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        var newSeries = Assert.Single(store.Lessons, lesson => lesson.Id != series.Id);
        Assert.Equal(newSeries.Id, result.Value!.Id);
        Assert.Equal((series.TeacherUserId, series.StudentId, series.Subject, series.LessonFormat, series.TimeZone),
            (newSeries.TeacherUserId, newSeries.StudentId, newSeries.Subject, newSeries.LessonFormat, newSeries.TimeZone));

        var oldOccurrences = Expand(series, store.Exceptions).ToArray();
        Assert.Equal(new[] { Anchor, Anchor.AddDays(7).AddHours(1), Anchor.AddDays(14) },
            oldOccurrences.Select(item => item.StartAtUtc));

        var newOccurrences = Expand(newSeries, store.Exceptions).ToArray();
        Assert.Equal(7, newOccurrences.Length);
        Assert.Equal(NewStart, newOccurrences[0].StartAtUtc);
        Assert.Equal(Anchor.AddDays(63).AddHours(2), newOccurrences[^1].StartAtUtc);

        Assert.Equal(series.Id, pastException.SeriesLessonScheduleId);
        Assert.Equal(Anchor.AddDays(7), pastException.OriginalStartAtUtc);

        Assert.Equal(newSeries.Id, cancelled.SeriesLessonScheduleId);
        Assert.Equal(Anchor.AddDays(35).AddHours(2), cancelled.OriginalStartAtUtc);
        Assert.Equal(OccurrenceExceptionAction.Cancelled, cancelled.Action);
        Assert.Null(cancelled.OverrideStartAtUtc);

        Assert.Equal(newSeries.Id, moved.SeriesLessonScheduleId);
        Assert.Equal(Anchor.AddDays(49).AddHours(2), moved.OriginalStartAtUtc);
        Assert.Equal(Anchor.AddDays(50).AddHours(2), moved.OverrideStartAtUtc);
        Assert.Equal(Anchor.AddDays(50).AddHours(3), moved.OverrideEndAtUtc);

        var split = Assert.Single(series.DomainEvents.OfType<LessonSeriesSplitDomainEvent>());
        Assert.Equal((series.Id, newSeries.Id, Cutoff), (split.OldId, split.NewId, split.CutoffUtc));
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Split_rejects_a_conflict_with_another_lesson_without_mutating_either_series()
    {
        var series = NewSeries();
        series.ClearDomainEvents();
        var blocker = new LessonSchedule(
            Guid.NewGuid(), series.TeacherUserId!.Value, Guid.NewGuid(), "Fizik", ScheduledLessonFormat.Online,
            NewStart, NewStart.AddHours(1), "Europe/Istanbul", null, LessonScheduleStatus.Planned, 30,
            null, null, null, Clock.UtcNow);
        blocker.ClearDomainEvents();
        var store = new Store(series, blocker);

        var result = await Handler(store).Handle(new RescheduleLessonScheduleCommand(
            series.Id, NewStart, NewStart.AddHours(1), null,
            OccurrenceScope.ThisAndFuture, Cutoff), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("scheduling.teacher_conflict", result.Error.Code);
        Assert.Equal(2, store.Lessons.Count);
        Assert.Equal(Anchor, series.StartAtUtc);
        Assert.Equal("FREQ=WEEKLY;BYDAY=MO;COUNT=10", series.RecurrenceRule);
        Assert.Empty(series.DomainEvents);
        Assert.Equal(0, store.SaveCount);
    }

    [Theory]
    [InlineData("FREQ=DAILY;COUNT=5", 1)]
    [InlineData("FREQ=WEEKLY;COUNT=5", 7)]
    public void End_series_preserves_every_occurrence_before_microsecond_cutoff(string rule, int days)
    {
        var start = Anchor.AddTicks(1234560);
        var series = new LessonSchedule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, start, start.AddHours(1), "Europe/Istanbul", rule,
            LessonScheduleStatus.Planned, 15, null, null, null, Clock.UtcNow);
        var cutoff = start.AddDays(2 * days);
        series.EndSeriesBefore(cutoff, Clock.UtcNow);
        var actual = RecurrenceExpander.Expand(series.StartAtUtc, series.EndAtUtc, series.RecurrenceRule,
            Calendar.Zone, start.AddDays(-1), cutoff.AddDays(30));
        Assert.Equal(new[] { start, start.AddDays(days) }, actual.Select(o => o.RecurrenceIdUtc));
    }

    [Fact]
    public async Task Moving_split_to_tuesday_keeps_selected_lesson_and_shifted_cancellation()
    {
        var series = NewSeries();
        var store = new Store(series);
        var cancellation = new LessonOccurrenceException(Guid.NewGuid(), series.Id, Anchor.AddDays(35),
            OccurrenceExceptionAction.Cancelled, null, null, "İptal", Clock.UtcNow);
        store.Exceptions.Add(cancellation);
        var movedStart = Cutoff.AddDays(1).AddHours(2);
        var result = await Handler(store).Handle(new RescheduleLessonScheduleCommand(series.Id,
            movedStart, movedStart.AddHours(1), null, OccurrenceScope.ThisAndFuture, Cutoff), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error.Code);
        var future = Assert.Single(store.Lessons, item => item.Id != series.Id);
        var occurrences = Expand(future, store.Exceptions).ToArray();
        Assert.Equal(Enumerable.Range(0, 7).Select(n => movedStart.AddDays(7 * n)), occurrences.Select(o => o.RecurrenceIdUtc));
        Assert.True(Assert.Single(occurrences, o => o.RecurrenceIdUtc == movedStart.AddDays(14)).IsCancelled);
    }

    [Theory]
    [InlineData("20261123T140000Z")]
    [InlineData("20261123T170000")]
    [InlineData("20261123")]
    public async Task Moving_last_until_occurrence_to_next_day_does_not_lose_it(string until)
    {
        var series = new LessonSchedule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, Anchor, Anchor.AddHours(1), "Europe/Istanbul",
            $"FREQ=WEEKLY;UNTIL={until}", LessonScheduleStatus.Planned, 15, null, null, null, Clock.UtcNow);
        var store = new Store(series);
        var movedStart = Cutoff.AddDays(1).AddHours(2);
        var result = await Handler(store).Handle(new RescheduleLessonScheduleCommand(series.Id,
            movedStart, movedStart.AddHours(1), null, OccurrenceScope.ThisAndFuture, Cutoff), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error.Code);
        var future = Assert.Single(store.Lessons, item => item.Id != series.Id);
        Assert.Equal(movedStart, Assert.Single(Expand(future, store.Exceptions)).StartAtUtc);
        Assert.Equal(3, Expand(series, store.Exceptions).Count());
    }

    private static LessonSchedule NewSeries()
        => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik", ScheduledLessonFormat.Online,
            Anchor, Anchor.AddHours(1), "Europe/Istanbul", "FREQ=WEEKLY;BYDAY=MO;COUNT=10",
            LessonScheduleStatus.Planned, 60, "Zoom", "https://meet.example/lesson", "Notlar", Clock.UtcNow);

    private static RescheduleLessonScheduleCommandHandler Handler(Store store)
        => new(
            store, new InMemoryTimeOffRepository(), new PassThroughScheduleLock(), Calendar,
            SchedulingConflictSettings.Default, Clock, new SequentialIds());

    private static IEnumerable<ScheduleOccurrence> Expand(LessonSchedule series, IReadOnlyCollection<LessonOccurrenceException> allExceptions)
        => RecurrenceExpander.Expand(
                series.StartAtUtc, series.EndAtUtc, series.RecurrenceRule, Calendar.SeriesZone(series.TimeZone),
                Anchor.AddDays(-1), Anchor.AddDays(90),
                allExceptions.Where(item => item.SeriesLessonScheduleId == series.Id)
                    .Select(item => new OccurrenceOverride(
                        item.OriginalStartAtUtc, item.Action, item.OverrideStartAtUtc, item.OverrideEndAtUtc))
                    .ToArray())
            .OrderBy(item => item.StartAtUtc);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class SequentialIds : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class Store(params LessonSchedule[] lessons) : ILessonScheduleRepository
    {
        public List<LessonSchedule> Lessons { get; } = [.. lessons];
        public List<LessonOccurrenceException> Exceptions { get; } = new();
        public int SaveCount { get; private set; }

        public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken)
            => Task.FromResult(Lessons.FirstOrDefault(lesson => lesson.Id == lessonId));

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForTeacherUntilAsync(
            Guid teacherUserId, DateTime untilUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonSchedule>>(Lessons
                .Where(lesson => lesson.TeacherUserId == teacherUserId
                    && lesson.Status != LessonScheduleStatus.Cancelled
                    && lesson.StartAtUtc <= untilUtc)
                .ToArray());

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(
            Guid teacherUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonOccurrenceException>>(Exceptions
                .Where(item => Lessons.Any(lesson => lesson.Id == item.SeriesLessonScheduleId && lesson.TeacherUserId == teacherUserId))
                .ToArray());

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(
            Guid seriesLessonScheduleId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonOccurrenceException>>(
                Exceptions.Where(item => item.SeriesLessonScheduleId == seriesLessonScheduleId).ToArray());

        public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken)
        {
            Lessons.Add(lessonSchedule);
            return Task.CompletedTask;
        }

        public Task UpsertExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(
            Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(
            Guid studentId, DateTime untilUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public void Remove(LessonSchedule lessonSchedule) => Lessons.Remove(lessonSchedule);
    }
}
