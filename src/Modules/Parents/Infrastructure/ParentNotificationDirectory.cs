using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Parents.Infrastructure;

internal sealed class ParentNotificationDirectory : IParentNotificationDirectory
{
    private readonly ParentsDbContext _dbContext;

    public ParentNotificationDirectory(ParentsDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyCollection<ParentNotificationTarget>> GetApprovedParentsForStudentAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var rows = await Query()
            .Where(x => x.link.StudentId == studentId)
            .ToArrayAsync(cancellationToken);

        return rows.Select(x => ToTarget(x.profile)).ToArray();
    }

    public async Task<IReadOnlyCollection<ParentStudentNotificationTarget>> ListAllApprovedTargetsAsync(CancellationToken cancellationToken)
    {
        var rows = await Query().ToArrayAsync(cancellationToken);
        return rows.Select(x => new ParentStudentNotificationTarget(x.link.StudentId, ToTarget(x.profile))).ToArray();
    }

    private IQueryable<LinkProfile> Query()
        => from link in _dbContext.ParentChildLinks
           where link.Status == ParentChildLinkStatus.Approved
           join profile in _dbContext.ParentProfiles on link.ParentUserId equals profile.UserId
           select new LinkProfile(link, profile);

    private static ParentNotificationTarget ToTarget(ParentProfile profile)
        => new(
            profile.UserId,
            profile.MembershipTier,
            new ParentNotificationPrefs(
                profile.NotifyMissedAssignment,
                profile.NotifyWeeklyProgressSummary,
                profile.NotifyLessonReminders,
                profile.NotifyTestResults,
                profile.NotifyPayments));

    private sealed record LinkProfile(ParentChildLink link, ParentProfile profile);
}
