namespace EgitimUssu.Shared.Contracts;

/// <summary>
/// K-09: Bir kullanıcının hak ettiği özellikler. <b>Tek kaynak Membership modülüdür</b>; hiçbir modül
/// kendi <c>MembershipTier</c> alanına bakarak karar vermez, bu kaydı okur.
/// </summary>
/// <param name="Tier">Etkin üyelik seviyesi.</param>
/// <param name="EffectiveUntilUtc">Premium'un geçerli olduğu son an; Free ise <c>null</c>.</param>
/// <param name="MaxStudents">Bir öğretmenin bağlayabileceği en fazla öğrenci (Free 5, Premium sınırsız).</param>
/// <param name="CanSeeAds">Kullanıcıya reklam gösterilir mi (Free true, Premium false).</param>
/// <param name="DetailedAnalytics">Aylık analiz / hedef takibi / konu zayıflığı gibi derinlik özellikleri.</param>
/// <param name="PdfReports">PDF gelişim raporu üretebilir mi.</param>
/// <param name="RichNotifications">Zengin bildirimler (haftalık özet, ödev/ödeme uyarıları) açık mı.</param>
public sealed record Entitlement(
    MembershipTier Tier,
    DateTime? EffectiveUntilUtc,
    int MaxStudents,
    bool CanSeeAds,
    bool DetailedAnalytics,
    bool PdfReports,
    bool RichNotifications);

/// <summary>
/// Kullanıcının hak ettiği özellikler. Tek kaynak: Membership modülü.
/// <para>
/// <b>Fail-safe</b>: kaynak okunamazsa istisna fırlatılmaz, <see cref="MembershipTier.Free"/> hakları
/// döner — premium özellik açılmaz ama çağıran işlem hata vermez.
/// </para>
/// <para>
/// ⚠️ Plandaki ayrı <c>MembershipTierContract</c> enum'u bilinçli olarak <b>yazılmadı</b>: aynı anlamı
/// taşıyan <see cref="MembershipTier"/> zaten bu ad alanında ve tüm modüller onu kullanıyor
/// (Öğrenilenler #100 / P09 Task 1 ile aynı gerekçe).
/// </para>
/// </summary>
public interface IEntitlementDirectory
{
    /// <summary>Tek kullanıcının haklarını döner.</summary>
    /// <param name="userId">Kullanıcı kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Kullanıcının hakları; kayıt yoksa Free.</returns>
    Task<Entitlement> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Birden çok kullanıcının haklarını tek turda döner (toplu tarama uçları için; ör. haftalık
    /// veli özeti). İstenen her kimlik sonuçta bulunur — kaydı olmayan kullanıcı Free döner.
    /// </summary>
    /// <param name="userIds">Kullanıcı kimlikleri.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Kimlik → hak eşlemesi.</returns>
    Task<IReadOnlyDictionary<Guid, Entitlement>> GetManyAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
