namespace EgitimUssu.Shared.Contracts;

public sealed record UpcomingLesson(Guid LessonScheduleId, string Subject, DateTime StartAtUtc, DateTime EndAtUtc);

public sealed record LastLessonSummary(Guid LessonSessionId, string TopicTitle, string? TeacherNotes, DateTime? CompletedOnUtc);

// Yaklaşan dersler — Scheduling uygular (LessonSchedule).
public interface IStudentUpcomingLessonsDirectory
{
    Task<IReadOnlyCollection<UpcomingLesson>> GetUpcomingAsync(Guid studentId, DateTime fromUtc, int take, CancellationToken cancellationToken);
}

// Son tamamlanan ders özeti — LessonSessions uygular. TeacherNotes veli-görünürlük garantisi olmadığından
// bu özet için doldurulmaz (null); veliye görünür öğretmen notları ayrı IStudentNotesDirectory (Veli V-F Task 3) ile döner.
public interface IStudentLastLessonDirectory
{
    Task<LastLessonSummary?> GetLastCompletedAsync(Guid studentId, CancellationToken cancellationToken);
}
