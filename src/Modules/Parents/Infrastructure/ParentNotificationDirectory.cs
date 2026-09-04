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
        // ⚠️ Filtre **projeksiyondan önce** uygulanır. Iki varligi kendi tanimladigimiz bir record'a
        // projekte edip sonra `x.link.StudentId` ile filtrelemek EF Core'da cevrilemiyor
        // (record'un icini goremez) → tum outbox zinciri gercek Postgres'te patliyordu.
        var profiles = await ApprovedLinks()
            .Where(link => link.StudentId == studentId)
            .Join(
                _dbContext.ParentProfiles,
                link => link.ParentUserId,
                profile => profile.UserId,
                (link, profile) => profile)
            .ToArrayAsync(cancellationToken);

        return profiles.Select(ToTarget).ToArray();
    }

    public async Task<IReadOnlyCollection<ParentStudentNotificationTarget>> ListAllApprovedTargetsAsync(CancellationToken cancellationToken)
    {
        var rows = await ApprovedLinks()
            .Join(
                _dbContext.ParentProfiles,
                link => link.ParentUserId,
                profile => profile.UserId,
                (link, profile) => new { link.StudentId, Profile = profile })
            .ToArrayAsync(cancellationToken);

        return rows.Select(row => new ParentStudentNotificationTarget(row.StudentId, ToTarget(row.Profile))).ToArray();
    }

    private IQueryable<ParentChildLink> ApprovedLinks()
        => _dbContext.ParentChildLinks.Where(link => link.Status == ParentChildLinkStatus.Approved);

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
}
