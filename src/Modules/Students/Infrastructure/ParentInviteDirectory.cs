using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

internal sealed class ParentInviteDirectory : IParentInviteDirectory
{
    private readonly StudentsDbContext _dbContext;
    private readonly IClock _clock;

    public ParentInviteDirectory(StudentsDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<ParentInviteInfo?> ResolveAsync(string inviteCode, CancellationToken cancellationToken)
    {
        var invite = await _dbContext.StudentParentInvites
            .FirstOrDefaultAsync(i => i.InviteCode == inviteCode && i.Status == ParentInviteStatus.Pending, cancellationToken);
        return invite is null ? null : new ParentInviteInfo(invite.Id, invite.StudentId, invite.ChildDisplayName);
    }

    public async Task MarkClaimedAsync(Guid inviteId, Guid parentUserId, CancellationToken cancellationToken)
    {
        var invite = await _dbContext.StudentParentInvites.FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken);
        if (invite is null)
        {
            return;
        }

        invite.Claim(parentUserId, _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
