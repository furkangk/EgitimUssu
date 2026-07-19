namespace EgitimUssu.Shared.Contracts;

public sealed record ParentVisibleNote(Guid Id, string Content, DateTime CreatedOnUtc);

public interface IStudentNotesDirectory
{
    // Veliye görünür öğretmen notları: LessonNoteVisibility ∈ {Student, StudentAndParent} (karar 2026-07-19).
    // Private notlar ASLA dönmez.
    Task<IReadOnlyCollection<ParentVisibleNote>> GetParentVisibleNotesAsync(Guid studentId, int take, CancellationToken cancellationToken);
}
