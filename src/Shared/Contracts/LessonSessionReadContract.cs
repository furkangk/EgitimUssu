namespace EgitimUssu.Shared.Contracts;

// Y2: Modüller-arası salt-okunur (read) sözleşme. LessonSessions bu sözleşmeyi uygular; Assignments tüketir.
// Böylece modüller birbirine doğrudan proje referansı vermez (anti-corruption / paylaşılan kontrat).

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
