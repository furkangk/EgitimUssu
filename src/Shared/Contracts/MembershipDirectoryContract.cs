namespace EgitimUssu.Shared.Contracts;

/// <summary>
/// Üyelik (Free/Premium) seviyesi. Free = çekirdek alışkanlık; Premium = derinlik (Ö-D §14.3).
/// Seviyenin kaynağı M17 (Membership) modülüdür; hak kontrolü için <see cref="IEntitlementDirectory"/> okunur.
/// </summary>
public enum MembershipTier
{
    Free = 1,
    Premium = 2
}

/// <summary>
/// ⚠️ <b>LEGACY — P12'de kaldırılacak.</b> K-09'dan (P09 Task 2) sonra üyeliğin tek kaynağı
/// <see cref="IEntitlementDirectory"/>'dir; kapılar artık bu sözleşmeyi <b>okumaz</b>.
/// <para>
/// Sözleşme yalnız <b>geçiş köprüsü</b> olarak yaşıyor: eski profil alanlarını
/// (<c>StudentProfile.MembershipTier</c>, <c>ParentProfile.MembershipTier</c>) Membership modülüne
/// taşır ve <c>EntitlementDirectory</c> dışında hiçbir tüketicisi yoktur. Bu yüzden birden çok
/// uygulaması aynı anda kayıtlıdır (Students + Parents) ve <c>IEnumerable&lt;IMembershipDirectory&gt;</c>
/// olarak çözülür — tekil olarak enjekte etme.
/// </para>
/// </summary>
public interface IMembershipDirectory
{
    /// <summary>
    /// Verilen kullanıcının üyelik seviyesini döner. Profil bulunamazsa <see cref="MembershipTier.Free"/> döner.
    /// </summary>
    Task<MembershipTier> GetTierAsync(Guid userId, CancellationToken cancellationToken);
}
