using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class SchedulingMutationIntegrityTests
{
    private static readonly DateTime Start = new(2026, 10, 16, 13, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(LessonScheduleStatus.Completed, OccurrenceScope.All)]
    [InlineData(LessonScheduleStatus.Cancelled, OccurrenceScope.All)]
    [InlineData(LessonScheduleStatus.Completed, OccurrenceScope.Single)]
    [InlineData(LessonScheduleStatus.Completed, OccurrenceScope.ThisAndFuture)]
    public async Task Closed_lesson_cannot_be_cancelled_or_modify_series(LessonScheduleStatus status, OccurrenceScope scope)
    {
        var lesson = NewLesson(status);
        var repository = new RecordingRepository(lesson);
        var handler = new CancelLessonScheduleCommandHandler(repository, new PassThroughScheduleLock(), new LocalCalendar("Europe/Istanbul", new FakeClock()), new FakeClock(), new FakeIdGenerator());

        var result = await handler.Handle(new CancelLessonScheduleCommand(lesson.Id, CancellationReason.Other,
            false, null, scope, Start.AddDays(7)), default);

        Assert.True(result.IsFailure);
        Assert.Equal("scheduling.not_editable", result.Error.Code);
        Assert.Equal(status, lesson.Status);
        AssertUnchanged(lesson, repository);
    }

    [Theory]
    [InlineData(LessonScheduleStatus.Cancelled, "scheduling.not_editable")]
    [InlineData(LessonScheduleStatus.Completed, "scheduling.already_completed")]
    public async Task Closed_lesson_cannot_be_completed(LessonScheduleStatus status, string error)
    {
        var lesson = NewLesson(status);
        var repository = new RecordingRepository(lesson);
        var handler = new CompleteLessonScheduleCommandHandler(repository, new FakeClock());

        var result = await handler.Handle(new CompleteLessonScheduleCommand(lesson.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error.Code);
        Assert.Equal(status, lesson.Status);
        AssertUnchanged(lesson, repository);
    }

    [Theory]
    [InlineData(OccurrenceScope.Single)]
    [InlineData(OccurrenceScope.ThisAndFuture)]
    [InlineData((OccurrenceScope)99)]
    public async Task Incomplete_cancel_scope_does_not_cancel_entire_series(OccurrenceScope scope)
    {
        var lesson = NewLesson(LessonScheduleStatus.Planned);
        var repository = new RecordingRepository(lesson);
        var handler = new CancelLessonScheduleCommandHandler(repository, new PassThroughScheduleLock(), new LocalCalendar("Europe/Istanbul", new FakeClock()), new FakeClock(), new FakeIdGenerator());

        var result = await handler.Handle(new CancelLessonScheduleCommand(lesson.Id, CancellationReason.Other,
            false, null, scope), default);

        Assert.True(result.IsFailure);
        Assert.Equal("scheduling.invalid_request", result.Error.Code);
        Assert.Equal(LessonScheduleStatus.Planned, lesson.Status);
        AssertUnchanged(lesson, repository);
    }

    [Theory]
    [InlineData(OccurrenceScope.Single)]
    [InlineData(OccurrenceScope.ThisAndFuture)]
    [InlineData((OccurrenceScope)99)]
    public async Task Incomplete_reschedule_scope_does_not_move_entire_series(OccurrenceScope scope)
    {
        var lesson = NewLesson(LessonScheduleStatus.Planned);
        var repository = new RecordingRepository(lesson);
        var handler = new RescheduleLessonScheduleCommandHandler(repository, new InMemoryTimeOffRepository(), new PassThroughScheduleLock(),
            new LocalCalendar("Europe/Istanbul", new FakeClock()), SchedulingConflictSettings.Default, new FakeClock(), new FakeIdGenerator());

        var result = await handler.Handle(new RescheduleLessonScheduleCommand(lesson.Id,
            Start.AddHours(2), Start.AddHours(3), null, scope), default);

        Assert.True(result.IsFailure);
        Assert.Equal("scheduling.invalid_request", result.Error.Code);
        Assert.Equal(Start, lesson.StartAtUtc);
        AssertUnchanged(lesson, repository);
    }

    [Fact]
    public async Task Planned_lesson_can_still_be_completed()
    {
        var lesson = NewLesson(LessonScheduleStatus.Planned);
        var repository = new RecordingRepository(lesson);
        var handler = new CompleteLessonScheduleCommandHandler(repository, new FakeClock());

        var result = await handler.Handle(new CompleteLessonScheduleCommand(lesson.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(LessonScheduleStatus.Completed, lesson.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    private static LessonSchedule NewLesson(LessonScheduleStatus status)
    {
        var lesson = new LessonSchedule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, Start, Start.AddHours(1), "Europe/Istanbul",
            "FREQ=WEEKLY;BYDAY=FR", status, 60, null, null, null, Start.AddDays(-1));
        lesson.ClearDomainEvents();
        return lesson;
    }

    private static void AssertUnchanged(LessonSchedule lesson, RecordingRepository repository)
    {
        Assert.Empty(lesson.DomainEvents);
        Assert.Equal("FREQ=WEEKLY;BYDAY=FR", lesson.RecurrenceRule);
        Assert.Empty(repository.AddedExceptions);
        Assert.Equal(0, repository.SaveCount);
    }

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class RecordingRepository : ILessonScheduleRepository
    {
        private readonly LessonSchedule _series;
        public List<LessonOccurrenceException> AddedExceptions { get; } = new();

        public RecordingRepository(LessonSchedule series) => _series = series;

        public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken)
            => Task.FromResult<LessonSchedule?>(_series.Id == lessonId ? _series : null);

        public Task UpsertExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken)
        {
            AddedExceptions.Add(occurrenceException);
            return Task.CompletedTask;
        }

        public int SaveCount { get; private set; }
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForTeacherUntilAsync(Guid teacherUserId, DateTime untilUtc, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public void Remove(LessonSchedule lessonSchedule) => throw new NotImplementedException();

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
