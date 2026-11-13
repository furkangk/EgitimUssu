using EgitimUssu.Modules.Membership.Application;
using EgitimUssu.Modules.Membership.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Membership.Infrastructure;

/// <summary>
/// M17-1/M17-3: Membership modülünün yazma tarafı.
/// <para>
/// Yazma yolundaki okumalar bilinçli olarak <b>izlenen</b> (tracked) sorgulardır: handler aggregate
/// üzerinde bir metot çağırıp <c>SaveChangesAsync</c> diyor; <c>AsNoTracking</c> olsaydı değişiklik
/// sessizce kaybolurdu. Yalnız plan listeleme (saf okuma) izlemesizdir.
/// </para>
/// </summary>
internal sealed class MembershipRepository : IMembershipRepository
{
    private readonly MembershipDbContext _dbContext;

    public MembershipRepository(MembershipDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<SubscriptionPlan>> ListActivePlansAsync(
        string? role, CancellationToken cancellationToken)
        => await _dbContext.SubscriptionPlans
            .AsNoTracking()
            .Where(plan => plan.IsActive && (role == null || plan.Role == role))
            .OrderBy(plan => plan.Role)
            .ThenBy(plan => plan.Period)
            .ToArrayAsync(cancellationToken);

    public Task<SubscriptionPlan?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken)
        => _dbContext.SubscriptionPlans.FirstOrDefaultAsync(plan => plan.Code == code, cancellationToken);

    public Task<SubscriptionPlan?> GetPlanByIdAsync(Guid planId, CancellationToken cancellationToken)
        => _dbContext.SubscriptionPlans.FirstOrDefaultAsync(plan => plan.Id == planId, cancellationToken);

    public async Task AddPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken)
        => await _dbContext.SubscriptionPlans.AddAsync(plan, cancellationToken);

    public Task<UserSubscription?> GetSubscriptionByUserAsync(Guid userId, CancellationToken cancellationToken)
        => _dbContext.UserSubscriptions.FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);

    public async Task AddSubscriptionAsync(UserSubscription subscription, CancellationToken cancellationToken)
        => await _dbContext.UserSubscriptions.AddAsync(subscription, cancellationToken);

    public Task<ReferralCode?> GetReferralCodeByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken)
        => _dbContext.ReferralCodes.FirstOrDefaultAsync(
            code => code.OwnerUserId == ownerUserId, cancellationToken);

    public Task<ReferralCode?> GetReferralCodeByCodeAsync(string code, CancellationToken cancellationToken)
        => _dbContext.ReferralCodes.FirstOrDefaultAsync(row => row.Code == code, cancellationToken);

    public async Task AddReferralCodeAsync(ReferralCode referralCode, CancellationToken cancellationToken)
        => await _dbContext.ReferralCodes.AddAsync(referralCode, cancellationToken);

    public Task<bool> HasRedeemedAsync(Guid redeemerUserId, CancellationToken cancellationToken)
        => _dbContext.ReferralRedemptions
            .AsNoTracking()
            .AnyAsync(row => row.RedeemerUserId == redeemerUserId, cancellationToken);

    public async Task AddRedemptionAsync(ReferralRedemption redemption, CancellationToken cancellationToken)
        => await _dbContext.ReferralRedemptions.AddAsync(redemption, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
