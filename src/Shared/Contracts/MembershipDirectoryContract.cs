namespace EgitimUssu.Shared.Contracts;

/// <summary>
/// Öğrencinin üyelik (Free/Premium) seviyesi. Free = çekirdek alışkanlık; Premium = derinlik (Ö-D §14.3).
/// M17 (Membership) tam modülü gelene kadar Students modülünde hafifçe tutulur.
/// </summary>
public enum MembershipTier
{
    Free = 1,
    Premium = 2
}

/// <summary>
/// Modüller-arası salt-okunur üyelik sözleşmesi. Students bu sözleşmeyi uygular; Study gibi diğer
/// modüller Free/Premium kapılarını bu sözleşmeden okuyarak Students'a doğrudan referans vermez.
/// </summary>
public interface IMembershipDirectory
{
    /// <summary>
    /// Verilen kullanıcının üyelik seviyesini döner. Profil bulunamazsa <see cref="MembershipTier.Free"/> döner.
    /// </summary>
    Task<MembershipTier> GetTierAsync(Guid userId, CancellationToken cancellationToken);
}
