using EgitimUssu.Modules.Membership.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Modules.Membership.Infrastructure;

/// <summary>
/// K-09: <see cref="IEntitlementDirectory"/>'nin tek uygulaması — üyelik hakları burada, aboneliğe
/// bakılarak hesaplanır.
/// <para>
/// <b>Geçiş köprüsü (P12'de kaldırılacak):</b> abonelik kaydı yokken eski üyelik alanları
/// (<c>StudentProfile.MembershipTier</c>, <c>ParentProfile.MembershipTier</c>) hâlâ okunur —
/// <see cref="IMembershipDirectory"/> uygulamaları bu köprünün girdisidir. Köprü olmasaydı, abonelik
/// akışı (Task 3–4) devreye girene kadar admin'in verdiği premium sessizce kaybolurdu. Karar noktası
/// yine de tektir: hiçbir modül o alanları kendisi okumaz, hepsi bu sınıfa sorar.
/// </para>
/// <para>
/// <b>Fail-safe</b>: <see cref="GetAsync"/> / <see cref="GetManyAsync"/> hata yutar ve Free döner
/// (premium açılmaz, çağıran iş akışı kırılmaz). Modülün kendi ucu olan <see cref="GetSnapshotAsync"/>
/// bunu yapmaz — kendi verisi okunamıyorsa kullanıcıya yanlış bilgi vermek yerine hata döner.
/// </para>
/// </summary>
internal sealed class EntitlementDirectory : IEntitlementDirectory, IMembershipReader
{
    private readonly MembershipDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IReadOnlyCollection<IMembershipDirectory> _legacyTierSources;
    private readonly ILogger<EntitlementDirectory> _logger;

    public EntitlementDirectory(
        MembershipDbContext dbContext,
        IClock clock,
        IEnumerable<IMembershipDirectory> legacyTierSources,
        ILogger<EntitlementDirectory> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _legacyTierSources = legacyTierSources.ToArray();
        _logger = logger;
    }

    public async Task<Entitlement> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await GetSnapshotAsync(userId, cancellationToken);
            return EntitlementPolicy.For(snapshot.Tier, snapshot.EffectiveUntilUtc);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Üyelik hakları okunamadı, Free varsayıldı. Kullanıcı: {UserId}", userId);
            return EntitlementPolicy.Free;
        }
    }

    public async Task<IReadOnlyDictionary<Guid, Entitlement>> GetManyAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, Entitlement>();
        }

        try
        {
            var now = _clock.UtcNow;
            var subscriptions = await _dbContext.UserSubscriptions
                .AsNoTracking()
                .Where(subscription => ids.Contains(subscription.UserId))
                .ToArrayAsync(cancellationToken);

            var result = new Dictionary<Guid, Entitlement>(ids.Length);
            foreach (var id in ids)
            {
                var subscription = Array.Find(subscriptions, row => row.UserId == id);
                if (subscription is not null && subscription.HasAccess(now))
                {
                    result[id] = EntitlementPolicy.For(
                        MembershipTier.Premium, subscription.CurrentPeriodEndUtc);
                    continue;
                }

                // Geçiş köprüsü: kayıt başına bir sorgu. P12'de eski alanlar silinince tek sorguya iner.
                result[id] = EntitlementPolicy.For(
                    await LegacyTierAsync(id, cancellationToken), null);
            }

            return result;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Toplu üyelik hakları okunamadı, {Count} kullanıcı Free varsayıldı.", ids.Length);
            return ids.ToDictionary(id => id, _ => EntitlementPolicy.Free);
        }
    }

    public async Task<UserMembershipSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var subscription = await _dbContext.UserSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);

        if (subscription is not null && subscription.HasAccess(now))
        {
            var planCode = await _dbContext.SubscriptionPlans
                .AsNoTracking()
                .Where(plan => plan.Id == subscription.PlanId)
                .Select(plan => plan.Code)
                .FirstOrDefaultAsync(cancellationToken);

            return new UserMembershipSnapshot(
                MembershipTier.Premium, subscription.CurrentPeriodEndUtc, subscription.Status, planCode);
        }

        var legacyTier = await LegacyTierAsync(userId, cancellationToken);

        // Köprüden gelen premium'un bitiş tarihi yoktur (eski alan yalnız seviye taşıyor).
        return new UserMembershipSnapshot(
            legacyTier, EffectiveUntilUtc: null, subscription?.Status, PlanCode: null);
    }

    /// <summary>Eski üyelik alanlarından en yüksek seviyeyi okur (geçici köprü).</summary>
    private async Task<MembershipTier> LegacyTierAsync(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var source in _legacyTierSources)
        {
            if (await source.GetTierAsync(userId, cancellationToken) == MembershipTier.Premium)
            {
                return MembershipTier.Premium;
            }
        }

        return MembershipTier.Free;
    }
}
