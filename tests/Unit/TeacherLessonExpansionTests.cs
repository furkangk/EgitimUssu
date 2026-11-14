using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Infrastructure;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// M04-1: Öğretmen ders listesi artık tekrar kuralını açar (occurrence üretir). Yanıttaki
/// <c>StartAtUtc/EndAtUtc</c> somut oluşumu, <c>OccurrenceStartAtUtc</c> ise iCal RECURRENCE-ID'yi
/// (istisna hedefleme anahtarı) taşır.
/// </summary>
public sealed class TeacherLessonExpansionTests
{
    private static readonly Guid TeacherUserId = Guid.NewGuid();
    private static readonly DateTime Anchor = new(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowStart = Anchor;
    private static readonly DateTime WindowEnd = Anchor.AddDays(22);
    private static readonly LocalCalendar Calendar = new("Europe/Istanbul", new FakeClock(Anchor));

    private static LessonSchedule Weekly(string? rule = "FREQ=WEEKLY")
        => new(
            Guid.NewGuid(), TeacherUserId, Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, Anchor, Anchor.AddHours(1), "Europe/Istanbul",
            rule, LessonScheduleStatus.Planned, 60, null, null, null, Anchor);

    [Theory]
    [InlineData("Europe/Istanbul")]
    [InlineData("Gecersiz/Dilim")] // çözülemeyen kimlik → platform dilimi (Europe/Istanbul), 500 değil
    public async Task Tuesday_night_series_lists_local_tuesdays(string seriesTimeZone)
    {
        // P16 T2 / N-04: Salı 01:30 TR = Pazartesi 22:30Z; BYDAY=TU yerel Salı'ya eşlenmeli.
        var tuesdayNight = new DateTime(2026, 11, 16, 22, 30, 0, DateTimeKind.Utc);
        var lesson = new LessonSchedule(
            Guid.NewGuid(), TeacherUserId, Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, tuesdayNight, tuesdayNight.AddHours(1), seriesTimeZone,
            "FREQ=WEEKLY;BYDAY=TU", LessonScheduleStatus.Planned, 60, null, null, null, tuesdayNight);
        var handler = new ListLessonSchedulesForTeacherQueryHandler(new FakeRepo(lesson), Calendar);

        var result = await handler.Handle(
            new ListLessonSchedulesForTeacherQuery(TeacherUserId, tuesdayNight, tuesdayNight.AddDays(14)), CancellationToken.None);

        Assert.Equal(
            new[] { tuesdayNight, tuesdayNight.AddDays(7), tuesdayNight.AddDays(14) },
            result.Value!.Select(item => item.StartAtUtc));
    }

    [Fact]
    public async Task Weekly_lesson_expands_into_four_occurrences()
    {
        var lesson = Weekly();
        var handler = new ListLessonSchedulesForTeacherQueryHandler(new FakeRepo(lesson), Calendar);

        var result = await handler.Handle(
            new ListLessonSchedulesForTeacherQuery(TeacherUserId, WindowStart, WindowEnd), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var items = result.Value!.ToArray();
        Assert.Equal(4, items.Length);
        Assert.All(items, item => Assert.Equal(lesson.Id, item.Id));
        Assert.Equal(
            new[] { Anchor, Anchor.AddDays(7), Anchor.AddDays(14), Anchor.AddDays(21) },
            items.Select(item => item.StartAtUtc));
        Assert.All(items, item => Assert.Equal(item.StartAtUtc, item.OccurrenceStartAtUtc));
        Assert.All(items, item => Assert.Equal(item.StartAtUtc.AddHours(1), item.EndAtUtc));
    }

    [Fact]
    public async Task Cancelled_occurrence_is_not_returned()
    {
        var lesson = Weekly();
        var cancelled = new LessonOccurrenceException(
            Guid.NewGuid(), lesson.Id, Anchor.AddDays(7),
            OccurrenceExceptionAction.Cancelled, null, null, null, Anchor);
        var handler = new ListLessonSchedulesForTeacherQueryHandler(new FakeRepo(new[] { lesson }, cancelled), Calendar);

        var result = await handler.Handle(
            new ListLessonSchedulesForTeacherQuery(TeacherUserId, WindowStart, WindowEnd), CancellationToken.None);

        var items = result.Value!.ToArray();
        Assert.Equal(3, items.Length);
        Assert.DoesNotContain(items, item => item.StartAtUtc == Anchor.AddDays(7));
    }

    [Fact]
    public async Task Rescheduled_occurrence_keeps_original_start_as_key()
    {
        var lesson = Weekly();
        var moved = new LessonOccurrenceException(
            Guid.NewGuid(), lesson.Id, Anchor.AddDays(14), OccurrenceExceptionAction.Rescheduled,
            Anchor.AddDays(14).AddHours(3), Anchor.AddDays(14).AddHours(4), null, Anchor);
        var handler = new ListLessonSchedulesForTeacherQueryHandler(new FakeRepo(new[] { lesson }, moved), Calendar);

        var result = await handler.Handle(
            new ListLessonSchedulesForTeacherQuery(TeacherUserId, WindowStart, WindowEnd), CancellationToken.None);

        var item = Assert.Single(result.Value!, occurrence => occurrence.StartAtUtc == Anchor.AddDays(14).AddHours(3));
        // Tek oluşumu iptal/erteleme uçları OriginalStartAtUtc ile hedefler; anahtar taşınmamalı.
        Assert.Equal(Anchor.AddDays(14), item.OccurrenceStartAtUtc);
    }

    [Fact]
    public async Task Non_recurring_lesson_returns_single_occurrence()
    {
        var lesson = Weekly(rule: null);
        var handler = new ListLessonSchedulesForTeacherQueryHandler(new FakeRepo(lesson), Calendar);

        var result = await handler.Handle(
            new ListLessonSchedulesForTeacherQuery(TeacherUserId, WindowStart, WindowEnd), CancellationToken.None);

        var item = Assert.Single(result.Value!);
        Assert.Equal(Anchor, item.StartAtUtc);
        Assert.Equal(Anchor, item.OccurrenceStartAtUtc);
    }

    [Fact]
    public async Task Series_ended_before_cutoff_stops_expanding()
    {
        var lesson = Weekly();
        lesson.EndSeriesBefore(Anchor.AddDays(14), Anchor);
        var handler = new ListLessonSchedulesForTeacherQueryHandler(new FakeRepo(lesson), Calendar);

        var result = await handler.Handle(
            new ListLessonSchedulesForTeacherQuery(TeacherUserId, WindowStart, WindowEnd), CancellationToken.None);

        Assert.Equal(2, result.Value!.Count);
    }

    private sealed class FakeRepo : ILessonScheduleRepository
    {
        private readonly LessonSchedule[] _lessons;
        private readonly LessonOccurrenceException[] _exceptions;

        public FakeRepo(params LessonSchedule[] lessons)
            : this(lessons, Array.Empty<LessonOccurrenceException>())
        {
        }

        public FakeRepo(LessonSchedule[] lessons, params LessonOccurrenceException[] exceptions)
        {
            _lessons = lessons;
            _exceptions = exceptions;
        }

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForTeacherUntilAsync(Guid teacherUserId, DateTime untilUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonSchedule>>(_lessons);

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonOccurrenceException>>(_exceptions);

        public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken) => throw new NotImplementedException();
        public void Remove(LessonSchedule lessonSchedule) => throw new NotImplementedException();
        public Task UpsertExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
