using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Parents.Infrastructure;

internal sealed class ParentNotificationDirectory : IParentNotificationDirectory
{
    private readonly ParentsDbContext _dbContext;
    private readonly IEntitlementDirectory _entitlements;

    public ParentNotificationDirectory(ParentsDbContext dbContext, IEntitlementDirectory entitlements)
    {
        _dbContext = dbContext;
        _entitlements = entitlements;
    }

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

        var tiers = await TiersAsync(profiles, cancellationToken);

        return profiles.Select(profile => ToTarget(profile, tiers)).ToArray();
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

        // Toplu tarama: üyelik tek turda okunur (veli başına ayrı sorgu değil).
        var tiers = await TiersAsync(rows.Select(row => row.Profile).ToArray(), cancellationToken);

        return rows
            .Select(row => new ParentStudentNotificationTarget(row.StudentId, ToTarget(row.Profile, tiers)))
            .ToArray();
    }

    private IQueryable<ParentChildLink> ApprovedLinks()
        => _dbContext.ParentChildLinks.Where(link => link.Status == ParentChildLinkStatus.Approved);

    /// <summary>
    /// K-09: Üyelik seviyesi <b>profil alanından değil</b>, tek kaynaktan (Membership) okunur.
    /// <c>ParentProfile.MembershipTier</c> yalnız geçiş köprüsünün girdisidir; karar bu değerle verilmez.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Entitlement>> TiersAsync(
        IReadOnlyCollection<ParentProfile> profiles, CancellationToken cancellationToken)
        => await _entitlements.GetManyAsync(
            profiles.Select(profile => profile.UserId).Distinct().ToArray(), cancellationToken);

    private static ParentNotificationTarget ToTarget(
        ParentProfile profile, IReadOnlyDictionary<Guid, Entitlement> tiers)
        => new(
            profile.UserId,
            tiers.TryGetValue(profile.UserId, out var entitlement) ? entitlement.Tier : MembershipTier.Free,
            new ParentNotificationPrefs(
                profile.NotifyMissedAssignment,
                profile.NotifyWeeklyProgressSummary,
                profile.NotifyLessonReminders,
                profile.NotifyTestResults,
                profile.NotifyPayments));
}
