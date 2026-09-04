using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Parents.Infrastructure;

/// <summary>
/// ⚠️ <b>GEÇİCİ KÖPRÜ (P12'de silinecek)</b> — <c>ParentProfile.MembershipTier</c> alanını
/// <see cref="IMembershipDirectory"/> üzerinden Membership modülüne taşır.
/// <para>
/// K-09'dan sonra üyeliğin tek kaynağı <c>membership.user_subscriptions</c>'tır; ama admin'in
/// <c>PUT /api/parents/{parentUserId}/membership-tier</c> ile verdiği premium hâlâ bu alanda duruyor.
/// Köprü olmasaydı o kayıtlar Task 3'teki abonelik akışı gelene kadar sessizce Free'ye düşerdi.
/// Karar noktası tek kalıyor: bu alanı yalnız <c>EntitlementDirectory</c> okur.
/// </para>
/// </summary>
internal sealed class ParentMembershipDirectory : IMembershipDirectory
{
    private readonly ParentsDbContext _dbContext;

    public ParentMembershipDirectory(ParentsDbContext dbContext) => _dbContext = dbContext;

    public async Task<MembershipTier> GetTierAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tier = await _dbContext.ParentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (MembershipTier?)profile.MembershipTier)
            .FirstOrDefaultAsync(cancellationToken);

        return tier ?? MembershipTier.Free;
    }
}
