using EgitimUssu.Modules.Teachers.Application;
using EgitimUssu.Modules.Teachers.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Teachers.Infrastructure;

internal sealed class TeacherProfileRepository : ITeacherProfileRepository
{
    private readonly TeachersDbContext _dbContext;

    public TeacherProfileRepository(TeachersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<TeacherProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.TeacherProfiles
            .Include(profile => profile.AvailabilitySlots)
            .Include(profile => profile.Subjects)
            .Include(profile => profile.Certificates)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
    }

    public Task AddAsync(TeacherProfile profile, CancellationToken cancellationToken)
    {
        return _dbContext.TeacherProfiles.AddAsync(profile, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
