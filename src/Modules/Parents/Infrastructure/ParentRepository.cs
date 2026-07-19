using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Modules.Parents.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Parents.Infrastructure;

internal sealed class ParentRepository : IParentRepository
{
    private readonly ParentsDbContext _dbContext;

    public ParentRepository(ParentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ParentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.ParentProfiles.FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
    }

    public Task<ParentChildLink?> GetLinkByIdAsync(Guid linkId, CancellationToken cancellationToken)
    {
        return _dbContext.ParentChildLinks.FirstOrDefaultAsync(link => link.Id == linkId, cancellationToken);
    }

    public Task<ParentChildLink?> GetActiveLinkAsync(Guid parentUserId, Guid studentId, CancellationToken cancellationToken)
    {
        // Aktif = iptal/reddedilmemiş; en güncel talebi döndürür (Pending veya Approved öncelikli).
        return _dbContext.ParentChildLinks
            .Where(link => link.ParentUserId == parentUserId
                && link.StudentId == studentId
                && (link.Status == ParentChildLinkStatus.Pending || link.Status == ParentChildLinkStatus.Approved))
            .OrderByDescending(link => link.Status == ParentChildLinkStatus.Approved)
            .ThenByDescending(link => link.RequestedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ParentChildLink>> ListLinksByParentAsync(Guid parentUserId, CancellationToken cancellationToken)
    {
        return await _dbContext.ParentChildLinks
            .Where(link => link.ParentUserId == parentUserId)
            .OrderByDescending(link => link.RequestedOnUtc)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ParentChildLink>> ListApprovedLinksForStudentAsync(Guid studentId, CancellationToken cancellationToken)
        => await _dbContext.ParentChildLinks
            .Where(l => l.StudentId == studentId && l.Status == ParentChildLinkStatus.Approved)
            .ToArrayAsync(cancellationToken);

    public Task<ChildProgressSnapshot?> GetSnapshotAsync(Guid studentId, CancellationToken cancellationToken)
    {
        return _dbContext.ChildProgressSnapshots.FirstOrDefaultAsync(snapshot => snapshot.StudentId == studentId, cancellationToken);
    }

    public Task<KnownStudent?> GetKnownStudentAsync(Guid studentId, CancellationToken cancellationToken)
    {
        return _dbContext.KnownStudents.FirstOrDefaultAsync(student => student.StudentId == studentId, cancellationToken);
    }

    public Task AddProfileAsync(ParentProfile profile, CancellationToken cancellationToken)
    {
        return _dbContext.ParentProfiles.AddAsync(profile, cancellationToken).AsTask();
    }

    public Task AddLinkAsync(ParentChildLink link, CancellationToken cancellationToken)
    {
        return _dbContext.ParentChildLinks.AddAsync(link, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
