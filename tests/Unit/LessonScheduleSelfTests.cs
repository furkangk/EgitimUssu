using EgitimUssu.Modules.Scheduling.Domain;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// Öğrencinin kendi dersi (öğretmensiz) için birleşik <see cref="LessonSchedule"/> davranışları (Ç-06).
/// Self ders = <c>TeacherUserId is null</c>; <c>LessonFormat</c> yok, konu/renk taşınır.
/// </summary>
public sealed class LessonScheduleSelfTests
{
    private static readonly DateTime Start = new(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateSelfLesson_sets_null_teacher_and_planned_status()
    {
        var studentId = Guid.NewGuid();

        var lesson = LessonSchedule.CreateSelfLesson(
            id: Guid.NewGuid(), studentId: studentId,
            subject: "Matematik", topic: "Türev",
            startAtUtc: Start, endAtUtc: Start.AddMinutes(60),
            timeZone: "Europe/Istanbul", recurrenceRule: null,
            reminderOffsetMinutes: 30, colorHex: "#20A4A9",
            notes: null, createdOnUtc: Start);

        Assert.Null(lesson.TeacherUserId);
        Assert.True(lesson.IsSelfPlanned);
        Assert.Null(lesson.LessonFormat);
        Assert.Equal(studentId, lesson.StudentId);
        Assert.Equal(LessonScheduleStatus.Planned, lesson.Status);
        Assert.Equal("Türev", lesson.Topic);
        Assert.Equal("#20A4A9", lesson.ColorHex);
        Assert.False(lesson.IsChargeable);
        Assert.Contains(lesson.DomainEvents, e => e is LessonScheduledDomainEvent);
    }

    [Fact]
    public void CreateSelfLesson_raises_event_with_null_teacher()
    {
        var lesson = LessonSchedule.CreateSelfLesson(
            Guid.NewGuid(), Guid.NewGuid(), "Fizik", null,
            Start, Start.AddMinutes(45), "Europe/Istanbul", null, 0, null, null, Start);

        var scheduled = Assert.IsType<LessonScheduledDomainEvent>(
            Assert.Single(lesson.DomainEvents, e => e is LessonScheduledDomainEvent));
        Assert.Null(scheduled.TeacherUserId);
        Assert.Equal(lesson.StudentId, scheduled.StudentId);
    }

    [Fact]
    public void TeacherLesson_is_not_self_planned()
    {
        var lesson = new LessonSchedule(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, Start, Start.AddHours(1), "Europe/Istanbul",
            null, LessonScheduleStatus.Planned, 60, null, null, null, Start);

        Assert.False(lesson.IsSelfPlanned);
        Assert.NotNull(lesson.TeacherUserId);
    }
}
