using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

/// <summary>
/// Veliye görünür öğretmen notlarını döner (Veli V-F). Yalnız `LessonNoteVisibility` ∈ {Student, StudentAndParent}
/// (karar 2026-07-19). **`Private` notlar asla dönmez.** İçerik = `LessonNote.Summary`.
/// </summary>
internal sealed class StudentNotesDirectory : IStudentNotesDirectory
{
    private readonly AssignmentsDbContext _dbContext;

    public StudentNotesDirectory(AssignmentsDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyCollection<ParentVisibleNote>> GetParentVisibleNotesAsync(Guid studentId, int take, CancellationToken cancellationToken)
        => await _dbContext.LessonNotes
            .Where(n => n.StudentId == studentId
                && (n.Visibility == LessonNoteVisibility.Student || n.Visibility == LessonNoteVisibility.StudentAndParent))
            .OrderByDescending(n => n.CreatedOnUtc)
            .Take(take)
            .Select(n => new ParentVisibleNote(n.Id, n.Summary, n.CreatedOnUtc))
            .ToArrayAsync(cancellationToken);
}
