using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// Birleşik takvim (Ç-06 A7/B5): tek kaynak lesson_schedules'ten öğretmen (Teacher, salt-okunur) ve
/// öğrencinin kendi dersleri (Self, düzenlenebilir) türetilir; occurrence.Completed reader ile doldurulur.
/// </summary>
public sealed class StudentCalendarQueryTests
{
    private static readonly DateTime WindowStart = new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SelfStart = new(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime TeacherStart = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
    private static readonly LocalCalendar Calendar = new("Europe/Istanbul", new FakeClock(WindowStart));

    [Fact]
    public async Task Derives_source_editability_and_completed()
    {
        var studentId = Guid.NewGuid();

        var teacher = new LessonSchedule(
            Guid.NewGuid(), Guid.NewGuid(), studentId, "İngilizce",
            ScheduledLessonFormat.Online, TeacherStart, TeacherStart.AddHours(1), "Europe/Istanbul",
            null, LessonScheduleStatus.Planned, 60, null, null, null, TeacherStart);

        var self = LessonSchedule.CreateSelfLesson(
            Guid.NewGuid(), studentId, "Matematik", "Türev",
            SelfStart, SelfStart.AddHours(1), "Europe/Istanbul", null, 30, "#20A4A9", null, SelfStart);

        var repo = new FakeRepo(teacher, self);
        // Self ders o gün çalışılmış; öğretmen dersi çalışılmamış.
        var reader = new FakeCompletionReader(new PlanCompletion(self.Id, DateOnly.FromDateTime(SelfStart)));
        var handler = new GetStudentCalendarQueryHandler(repo, reader, Calendar);

        var result = await handler.Handle(new GetStudentCalendarQuery(studentId, WindowStart, WindowEnd), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var occ = result.Value!;
        Assert.Equal(2, occ.Count);

        var teacherOcc = Assert.Single(occ, o => o.Source == "Teacher");
        Assert.False(teacherOcc.IsEditable);
        Assert.False(teacherOcc.Completed);

        var selfOcc = Assert.Single(occ, o => o.Source == "Self");
        Assert.True(selfOcc.IsEditable);
        Assert.True(selfOcc.Completed);
        Assert.Equal("Türev", selfOcc.Topic);
        Assert.Equal("#20A4A9", selfOcc.ColorHex);
    }

    [Fact]
    public async Task Flags_self_entry_that_overlaps_a_teacher_lesson()
    {
        var studentId = Guid.NewGuid();

        var teacher = new LessonSchedule(
            Guid.NewGuid(), Guid.NewGuid(), studentId, "İngilizce",
            ScheduledLessonFormat.Online, TeacherStart, TeacherStart.AddHours(1), "Europe/Istanbul",
            null, LessonScheduleStatus.Planned, 60, null, null, null, TeacherStart);

        // Öğretmen dersinin ortasına denk gelen kendi planı (öğretmen dersi sonradan açılmış olabilir).
        var overlapping = LessonSchedule.CreateSelfLesson(
            Guid.NewGuid(), studentId, "Matematik", null,
            TeacherStart.AddMinutes(30), TeacherStart.AddMinutes(90), "Europe/Istanbul", null, 30, null, null, TeacherStart);

        var free = LessonSchedule.CreateSelfLesson(
            Guid.NewGuid(), studentId, "Fizik", null,
            SelfStart, SelfStart.AddHours(1), "Europe/Istanbul", null, 30, null, null, SelfStart);

        var handler = new GetStudentCalendarQueryHandler(new FakeRepo(teacher, overlapping, free), new FakeCompletionReader(), Calendar);

        var result = await handler.Handle(new GetStudentCalendarQuery(studentId, WindowStart, WindowEnd), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var occurrences = result.Value!;

        Assert.True(Assert.Single(occurrences, o => o.EntryId == overlapping.Id).ConflictsWithLesson);
        Assert.False(Assert.Single(occurrences, o => o.EntryId == free.Id).ConflictsWithLesson);
        // Bayrak yalnız bireysel plan öğelerinde anlamlıdır; öğretmen dersi kendisiyle çakışmaz.
        Assert.False(Assert.Single(occurrences, o => o.EntryId == teacher.Id).ConflictsWithLesson);
    }

    [Fact]
    public async Task Recurring_teacher_lesson_conflicts_with_later_self_occurrence()
    {
        var studentId = Guid.NewGuid();

        // Haftalık öğretmen dersi; çakışma ikinci haftada oluşuyor.
        var teacher = new LessonSchedule(
            Guid.NewGuid(), Guid.NewGuid(), studentId, "İngilizce",
            ScheduledLessonFormat.Online, TeacherStart, TeacherStart.AddHours(1), "Europe/Istanbul",
            "FREQ=WEEKLY", LessonScheduleStatus.Planned, 60, null, null, null, TeacherStart);

        var self = LessonSchedule.CreateSelfLesson(
            Guid.NewGuid(), studentId, "Matematik", null,
            TeacherStart.AddDays(7), TeacherStart.AddDays(7).AddHours(1), "Europe/Istanbul", null, 30, null, null, TeacherStart);

        var handler = new GetStudentCalendarQueryHandler(new FakeRepo(teacher, self), new FakeCompletionReader(), Calendar);

        var result = await handler.Handle(
            new GetStudentCalendarQuery(studentId, WindowStart, WindowEnd.AddDays(7)), CancellationToken.None);

        Assert.True(Assert.Single(result.Value!, o => o.EntryId == self.Id).ConflictsWithLesson);
    }

    private sealed class FakeCompletionReader : IStudyPlanCompletionReader
    {
        private readonly PlanCompletion[] _completions;
        public FakeCompletionReader(params PlanCompletion[] completions) => _completions = completions;

        public Task<IReadOnlyCollection<PlanCompletion>> GetCompletionsAsync(Guid studentId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<PlanCompletion>>(_completions);
    }

    private sealed class FakeRepo : ILessonScheduleRepository
    {
        private readonly LessonSchedule[] _lessons;
        public FakeRepo(params LessonSchedule[] lessons) => _lessons = lessons;

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonSchedule>>(_lessons);

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonOccurrenceException>>(Array.Empty<LessonOccurrenceException>());

        public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> HasTeacherConflictAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, Guid? excludeLessonId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonSchedule>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForTeacherUntilAsync(Guid teacherUserId, DateTime untilUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken) => throw new NotImplementedException();
        public void Remove(LessonSchedule lessonSchedule) => throw new NotImplementedException();
        public Task UpsertExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
