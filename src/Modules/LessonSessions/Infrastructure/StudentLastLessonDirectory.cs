using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.LessonSessions.Infrastructure;

/// <summary>
/// Veli paneli için öğrencinin son tamamlanan ders özetini döner (Veli V-F). Öğretmen notu (TeacherNotes)
/// veli-görünürlük garantisi olmadığından bu özette DOLDURULMAZ (null) — veliye görünür notlar ayrı contract ile.
/// </summary>
internal sealed class StudentLastLessonDirectory : IStudentLastLessonDirectory
{
    private readonly LessonSessionsDbContext _dbContext;

    public StudentLastLessonDirectory(LessonSessionsDbContext dbContext) => _dbContext = dbContext;

    public async Task<LastLessonSummary?> GetLastCompletedAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var last = await _dbContext.LessonSessions
            .Where(s => s.StudentId == studentId && s.Status == LessonSessionStatus.Completed)
            .OrderByDescending(s => s.CompletedOnUtc)
            .Select(s => new { s.Id, s.TopicTitle, s.CompletedOnUtc })
            .FirstOrDefaultAsync(cancellationToken);

        return last is null ? null : new LastLessonSummary(last.Id, last.TopicTitle, null, last.CompletedOnUtc);
    }
}
