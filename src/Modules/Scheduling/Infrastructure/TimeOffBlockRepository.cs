using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

internal sealed class TimeOffBlockRepository : ITimeOffBlockRepository
{
    private readonly SchedulingDbContext _dbContext;
    public TimeOffBlockRepository(SchedulingDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(TimeOffBlock block, CancellationToken cancellationToken)
        => _dbContext.TimeOffBlocks.AddAsync(block, cancellationToken).AsTask();

    public Task<TimeOffBlock?> GetByIdAsync(Guid timeOffId, CancellationToken cancellationToken)
        => _dbContext.TimeOffBlocks.FirstOrDefaultAsync(b => b.Id == timeOffId, cancellationToken);

    public async Task<IReadOnlyCollection<TimeOffBlock>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
        => await _dbContext.TimeOffBlocks
            .Where(b => b.TeacherUserId == teacherUserId && b.StartAtUtc < endAtUtc && b.EndAtUtc > startAtUtc)
            .ToArrayAsync(cancellationToken);

    public void Remove(TimeOffBlock block) => _dbContext.TimeOffBlocks.Remove(block);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
}
