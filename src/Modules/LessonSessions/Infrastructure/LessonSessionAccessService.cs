using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Modules.LessonSessions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.LessonSessions.Infrastructure;

internal sealed class LessonSessionAccessService : ILessonSessionAccessService
{
    private readonly LessonSessionsDbContext _dbContext;

    public LessonSessionAccessService(LessonSessionsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LessonSessionDetails?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
    {
        return await _dbContext.LessonSessions
            .AsNoTracking()
            .Where(session => session.Id == lessonSessionId)
            .Select(session => new LessonSessionDetails(
                session.Id,
                session.LessonScheduleId,
                session.TeacherUserId,
                session.StudentId,
                session.Status == LessonSessionStatus.Completed,
                session.CompletedOnUtc,
                session.TopicTitle,
                session.CoveredContent,
                session.TeacherNotes))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
