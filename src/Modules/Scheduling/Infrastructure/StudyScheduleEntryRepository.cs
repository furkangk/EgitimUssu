using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

internal sealed class StudyScheduleEntryRepository : IStudyScheduleEntryRepository
{
    private readonly SchedulingDbContext _dbContext;

    public StudyScheduleEntryRepository(SchedulingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<StudyScheduleEntry?> GetByIdAsync(Guid entryId, CancellationToken cancellationToken)
    {
        return _dbContext.StudyScheduleEntries.FirstOrDefaultAsync(entry => entry.Id == entryId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<StudyScheduleEntry>> ListActiveForStudentAsync(Guid studentId, CancellationToken cancellationToken)
    {
        return await _dbContext.StudyScheduleEntries
            .Where(entry => entry.StudentId == studentId && entry.Status == StudyScheduleEntryStatus.Active)
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(StudyScheduleEntry entry, CancellationToken cancellationToken)
    {
        return _dbContext.StudyScheduleEntries.AddAsync(entry, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
