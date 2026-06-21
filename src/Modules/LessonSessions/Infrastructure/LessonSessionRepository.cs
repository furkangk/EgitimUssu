using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Modules.LessonSessions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.LessonSessions.Infrastructure;

internal sealed class LessonSessionRepository : ILessonSessionRepository
{
    private readonly LessonSessionsDbContext _dbContext;

    public LessonSessionRepository(LessonSessionsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<LessonSession?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSessions.FirstOrDefaultAsync(session => session.Id == lessonSessionId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<LessonSession>> ListAsync(
        Guid? teacherUserId,
        Guid? studentId,
        DateTime? dateFromUtc,
        DateTime? dateToUtc,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.LessonSessions.AsQueryable();

        if (teacherUserId.HasValue)
        {
            query = query.Where(x => x.TeacherUserId == teacherUserId.Value);
        }

        if (studentId.HasValue)
        {
            query = query.Where(x => x.StudentId == studentId.Value);
        }

        if (dateFromUtc.HasValue)
        {
            query = query.Where(x => x.PlannedStartAtUtc >= dateFromUtc.Value);
        }

        if (dateToUtc.HasValue)
        {
            query = query.Where(x => x.PlannedStartAtUtc <= dateToUtc.Value);
        }

        return await query
            .OrderBy(x => x.PlannedStartAtUtc)
            .ThenBy(x => x.CreatedOnUtc)
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(LessonSession lessonSession, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSessions.AddAsync(lessonSession, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
