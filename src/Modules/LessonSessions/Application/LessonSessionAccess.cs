namespace EgitimUssu.Modules.LessonSessions.Application;

public sealed record LessonSessionDetails(
    Guid Id,
    Guid? LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    bool IsCompleted,
    DateTime? CompletedOnUtc,
    string TopicTitle,
    string? CoveredContent,
    string? TeacherNotes);

public interface ILessonSessionAccessService
{
    Task<LessonSessionDetails?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken);
}
