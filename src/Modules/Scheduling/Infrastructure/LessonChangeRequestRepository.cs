using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

internal sealed class LessonChangeRequestRepository : ILessonChangeRequestRepository
{
    private readonly SchedulingDbContext _dbContext;

    public LessonChangeRequestRepository(SchedulingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(LessonChangeRequest request, CancellationToken cancellationToken)
        => _dbContext.LessonChangeRequests.AddAsync(request, cancellationToken).AsTask();

    public async Task<LessonChangeRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.LessonChangeRequests.FindAsync(new object?[] { requestId }, cancellationToken);
        if (entity is not null && _dbContext.Database.CurrentTransaction is not null &&
            _dbContext.Entry(entity).State == EntityState.Unchanged)
            await _dbContext.Entry(entity).ReloadAsync(cancellationToken);
        return entity is not null && _dbContext.Entry(entity).State != EntityState.Detached ? entity : null;
    }

    public async Task<IReadOnlyCollection<LessonChangeRequest>> ListForTeacherAsync(Guid teacherUserId, bool onlyPending, CancellationToken cancellationToken)
        => await _dbContext.LessonChangeRequests
            .Where(request => request.TeacherUserId == teacherUserId
                && (!onlyPending || request.Status == LessonChangeRequestStatus.Pending))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<LessonChangeRequest>> ListForStudentAsync(Guid studentId, CancellationToken cancellationToken)
        => await _dbContext.LessonChangeRequests
            .Where(request => request.StudentId == studentId)
            .ToArrayAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
