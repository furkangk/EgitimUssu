using EgitimUssu.Modules.Teachers.Application;
using EgitimUssu.Modules.Teachers.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Teachers.Infrastructure;

internal sealed class TeacherProfileRepository : ITeacherProfileRepository, ITeacherProfileSearchRepository
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

    // M02-3: Filtre mantığı TeacherSearchFilter'da; burada yalnız Include + sayfalama var.
    // Böylece birim testindeki filtre ile üretimdeki EF sorgusu aynı ifadeyi paylaşır.
    public async Task<(IReadOnlyList<TeacherProfile> Items, int TotalCount)> SearchAsync(
        SearchTeacherProfilesQuery query, int skip, int take, CancellationToken cancellationToken)
    {
        var filtered = TeacherSearchFilter.Apply(
            _dbContext.TeacherProfiles.Include(profile => profile.Subjects), query);

        var totalCount = await filtered.CountAsync(cancellationToken);
        var items = await filtered.Skip(skip).Take(take).ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
