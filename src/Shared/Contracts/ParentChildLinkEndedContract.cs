namespace EgitimUssu.Shared.Contracts;

/// <summary>P15 / N-26: Veli–çocuk bağının neden bittiği.</summary>
public enum ParentChildLinkEndReason
{
    /// <summary>Bekleyen ya da onaylı bağ reddedildi (öğrenci/Admin).</summary>
    Rejected = 1,

    /// <summary>Bağ iptal edildi (veli, öğrenci ya da Admin).</summary>
    Revoked = 2
}

/// <summary>
/// P15 / N-26: Yaşayan (<c>Pending</c>/<c>Approved</c>) bir veli–çocuk bağı reddedildiğinde ya da iptal edildiğinde
/// Parents modülünün Outbox üzerinden yaydığı integration event yükü. Domain event adı
/// <c>ParentChildLinkEndedDomainEvent</c> ile eşleşir.
/// <para>
/// Students bu olayla profilin <c>ParentUserId</c>'sini (yalnız o veliyi gösteriyorsa) temizler. Erişim kararı bu
/// alana bağlı değildir — profil okuma onaylı bağı <see cref="IParentAccessDirectory"/>'den okur, iptal anında kesilir;
/// olay yalnız türetilmiş alanı ve onu okuyan bildirim/görünüm yollarını tutarlı tutar.
/// </para>
/// </summary>
/// <param name="LinkId">Biten bağ.</param>
/// <param name="ParentUserId">Bağın velisi.</param>
/// <param name="StudentId">Bağın öğrenci profili.</param>
/// <param name="Reason">Bitiş nedeni.</param>
/// <param name="EndedOnUtc">Bitiş anı.</param>
public sealed record ParentChildLinkEndedIntegrationEvent(
    Guid LinkId,
    Guid ParentUserId,
    Guid StudentId,
    ParentChildLinkEndReason Reason,
    DateTime EndedOnUtc);
