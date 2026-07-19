using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

/// <summary>Veli paneli için öğrencinin yaklaşan (Planned, gelecekteki) derslerini döner (Veli V-F).</summary>
internal sealed class StudentUpcomingLessonsDirectory : IStudentUpcomingLessonsDirectory
{
    private readonly SchedulingDbContext _dbContext;

    public StudentUpcomingLessonsDirectory(SchedulingDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyCollection<UpcomingLesson>> GetUpcomingAsync(Guid studentId, DateTime fromUtc, int take, CancellationToken cancellationToken)
        // Ç-06: veli paneli öğretmen derslerini gösterir; öğrencinin kendi dersleri (TeacherUserId null) hariç.
        => await _dbContext.LessonSchedules
            .Where(l => l.StudentId == studentId && l.TeacherUserId != null && l.Status == LessonScheduleStatus.Planned && l.StartAtUtc >= fromUtc)
            .OrderBy(l => l.StartAtUtc)
            .Take(take)
            .Select(l => new UpcomingLesson(l.Id, l.Subject, l.StartAtUtc, l.EndAtUtc))
            .ToArrayAsync(cancellationToken);
}
