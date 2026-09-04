using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Parents.Infrastructure;

internal sealed class ParentAccessDirectory : IParentAccessDirectory
{
    private readonly ParentsDbContext _dbContext;

    public ParentAccessDirectory(ParentsDbContext dbContext) => _dbContext = dbContext;

    public Task<bool> IsApprovedParentOfStudentAsync(Guid parentUserId, Guid studentId, CancellationToken cancellationToken)
        => _dbContext.ParentChildLinks.AnyAsync(
            l => l.ParentUserId == parentUserId && l.StudentId == studentId && l.Status == ParentChildLinkStatus.Approved,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> ListApprovedStudentIdsAsync(Guid parentUserId, CancellationToken cancellationToken)
        => await _dbContext.ParentChildLinks
            .Where(l => l.ParentUserId == parentUserId && l.Status == ParentChildLinkStatus.Approved)
            .Select(l => l.StudentId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
}
