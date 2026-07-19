using EgitimUssu.Modules.Study.Domain;

namespace EgitimUssu.Tests.Unit;

/// <summary>Seansın bir plana (LessonSchedule/self ders) gevşek bağlanması (Ç-06 B1).</summary>
public sealed class StudySessionLessonTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StartStopwatch_carries_lessonId()
    {
        var lessonId = Guid.NewGuid();
        var session = StudySession.StartStopwatch(
            Guid.NewGuid(), Guid.NewGuid(), "Matematik", "Türev",
            isSharedWithParent: false, isSharedWithTeacher: false, Now, lessonId);

        Assert.Equal(lessonId, session.LessonId);
    }

    [Fact]
    public void StartStopwatch_without_lesson_is_null()
    {
        var session = StudySession.StartStopwatch(
            Guid.NewGuid(), Guid.NewGuid(), "Matematik", null,
            isSharedWithParent: false, isSharedWithTeacher: false, Now);

        Assert.Null(session.LessonId);
    }

    [Fact]
    public void CreateManual_carries_lessonId()
    {
        var lessonId = Guid.NewGuid();
        var session = StudySession.CreateManual(
            Guid.NewGuid(), Guid.NewGuid(), "Fizik", null, 30, Now, null,
            isSharedWithParent: false, isSharedWithTeacher: false, Now, lessonId);

        Assert.Equal(lessonId, session.LessonId);
    }
}
