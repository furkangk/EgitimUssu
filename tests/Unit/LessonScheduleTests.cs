using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class LessonScheduleTests
{
    private static readonly DateTime Start = new(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 7, 20, 14, 0, 0, DateTimeKind.Utc);

    private static LessonSchedule NewLesson(string? meetingUrl = null, string? recurrenceRule = null)
        => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, Start, End, "Europe/Istanbul",
            recurrenceRule, LessonScheduleStatus.Planned, 60, "adres", meetingUrl, null, Start);

    [Fact]
    public void Ctor_StoresMeetingUrl()
    {
        var lesson = NewLesson(meetingUrl: "https://meet.example/abc");
        Assert.Equal("https://meet.example/abc", lesson.MeetingUrl);
    }

    [Fact]
    public void UpdateDetails_ChangesMeetingUrl()
    {
        var lesson = NewLesson(meetingUrl: "https://old");
        lesson.UpdateDetails("Matematik", ScheduledLessonFormat.Online, Start, End,
            "Europe/Istanbul", null, 60, "adres", "https://new", null, Start.AddMinutes(1));
        Assert.Equal("https://new", lesson.MeetingUrl);
    }

    [Fact]
    public void Reschedule_KeepsPlanned_SetsOriginalStartOnce_RaisesEvent()
    {
        var lesson = NewLesson();
        var newStart = Start.AddDays(2);
        var newEnd = End.AddDays(2);

        lesson.Reschedule(newStart, newEnd, "Öğrenci hasta", Start.AddHours(1));

        Assert.Equal(LessonScheduleStatus.Planned, lesson.Status);
        Assert.Equal(newStart, lesson.StartAtUtc);
        Assert.Equal(Start, lesson.OriginalStartAtUtc);
        Assert.Equal("Öğrenci hasta", lesson.RescheduleNote);
        Assert.Contains(lesson.DomainEvents, e => e is LessonScheduleRescheduledDomainEvent);

        // İkinci erteleme OriginalStart'ı değiştirmez
        lesson.Reschedule(newStart.AddDays(1), newEnd.AddDays(1), null, Start.AddHours(2));
        Assert.Equal(Start, lesson.OriginalStartAtUtc);
    }

    [Fact]
    public void Cancel_StoresReasonAndChargeable()
    {
        var lesson = NewLesson();
        lesson.Cancel(CancellationReason.StudentCancelled, isChargeable: true, "geç haber verdi", Start.AddHours(1));

        Assert.Equal(LessonScheduleStatus.Cancelled, lesson.Status);
        Assert.Equal(CancellationReason.StudentCancelled, lesson.CancellationReason);
        Assert.True(lesson.IsChargeable);
        Assert.Contains(lesson.DomainEvents, e => e is LessonScheduleCancelledDomainEvent);
    }

    [Fact]
    public void CanBeDeletedAt_Rules()
    {
        var created = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);
        var lessonStart = new DateTime(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc); // gelecekte
        var lesson = new LessonSchedule(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, lessonStart, lessonStart.AddHours(1), "Europe/Istanbul",
            null, LessonScheduleStatus.Planned, 60, null, null, null, created);

        Assert.True(lesson.CanBeDeletedAt(created.AddHours(1)));    // <24s, ders gelecekte
        Assert.False(lesson.CanBeDeletedAt(created.AddHours(25)));  // 24s aşıldı
        Assert.False(lesson.CanBeDeletedAt(lessonStart.AddMinutes(1))); // ders geçmişte
    }

    [Fact]
    public void OccurrenceException_Ctor_StoresFields()
    {
        var seriesId = Guid.NewGuid();
        var original = new DateTime(2026, 7, 27, 13, 0, 0, DateTimeKind.Utc);
        var ex = new LessonOccurrenceException(
            Guid.NewGuid(), seriesId, original, OccurrenceExceptionAction.Rescheduled,
            original.AddDays(1), original.AddDays(1).AddHours(1), "bir hafta ertelendi", original);

        Assert.Equal(seriesId, ex.SeriesLessonScheduleId);
        Assert.Equal(OccurrenceExceptionAction.Rescheduled, ex.Action);
        Assert.Equal(original.AddDays(1), ex.OverrideStartAtUtc);
    }

    [Fact]
    public async Task Cancel_SingleScope_OnRecurringLesson_WritesExceptionInsteadOfCancellingSeries()
    {
        var series = new LessonSchedule(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, Start, End, "Europe/Istanbul",
            "FREQ=WEEKLY;BYDAY=MO", LessonScheduleStatus.Planned, 60, null, null, null, Start);

        var repo = new RecordingRepository(series);
        var handler = new CancelLessonScheduleCommandHandler(repo, new FakeClock(), new FakeIdGenerator());

        var occurrence = Start.AddDays(7);
        var result = await handler.Handle(
            new CancelLessonScheduleCommand(series.Id, CancellationReason.StudentCancelled, false, null, OccurrenceScope.Single, occurrence),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(LessonScheduleStatus.Planned, series.Status); // seri bozulmadı
        Assert.Single(repo.AddedExceptions);
        Assert.Equal(OccurrenceExceptionAction.Cancelled, repo.AddedExceptions[0].Action);
        Assert.Equal(occurrence, repo.AddedExceptions[0].OriginalStartAtUtc);
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

        public Task AddExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken)
        {
            AddedExceptions.Add(occurrenceException);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> HasTeacherConflictAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, Guid? excludeLessonId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<LessonSchedule>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
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
