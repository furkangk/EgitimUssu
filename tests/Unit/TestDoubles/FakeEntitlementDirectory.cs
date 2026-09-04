using EgitimUssu.Modules.Membership.Application;
using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Tests.Unit.TestDoubles;

/// <summary>
/// K-09: Üyelik kapılarını test eden her yerin ortak sahtesi (Öğrenilenler #46 — sahteyi
/// test dosyasına gömme). Hakları <see cref="EntitlementPolicy"/> ile üretir, böylece testler
/// üretimdeki seviye→hak eşlemesinden sapmaz.
/// </summary>
public sealed class FakeEntitlementDirectory : IEntitlementDirectory
{
    private readonly Entitlement _entitlement;

    /// <summary>Verilen seviyenin standart haklarıyla sahte üretir.</summary>
    /// <param name="tier">Üyelik seviyesi.</param>
    public FakeEntitlementDirectory(MembershipTier tier) => _entitlement = EntitlementPolicy.For(tier, null);

    /// <summary>Elle kurulmuş haklarla sahte üretir (sınır senaryoları için).</summary>
    /// <param name="entitlement">Dönülecek haklar.</param>
    public FakeEntitlementDirectory(Entitlement entitlement) => _entitlement = entitlement;

    /// <inheritdoc />
    public Task<Entitlement> GetAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult(_entitlement);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, Entitlement>> GetManyAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<Guid, Entitlement>>(
            userIds.Distinct().ToDictionary(id => id, _ => _entitlement));
}
