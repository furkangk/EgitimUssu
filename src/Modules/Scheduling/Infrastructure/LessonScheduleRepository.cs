using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

internal sealed class LessonScheduleRepository : ILessonScheduleRepository
{
    private readonly SchedulingDbContext _dbContext;

    public LessonScheduleRepository(SchedulingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSchedules.FirstOrDefaultAsync(lesson => lesson.Id == lessonId, cancellationToken);
    }

    public Task<bool> HasTeacherConflictAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSchedules.AnyAsync(
            lesson => lesson.TeacherUserId == teacherUserId
                && lesson.Status != LessonScheduleStatus.Cancelled
                && lesson.StartAtUtc < endAtUtc
                && lesson.EndAtUtc > startAtUtc,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<LessonSchedule>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
    {
        return await _dbContext.LessonSchedules
            .Where(lesson => lesson.TeacherUserId == teacherUserId
                && lesson.StartAtUtc >= startAtUtc
                && lesson.StartAtUtc <= endAtUtc)
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSchedules.AddAsync(lessonSchedule, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
