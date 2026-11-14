namespace EgitimUssu.Shared.Contracts;

/// <summary>Bir veli davetinin çözümlenmiş hâli.</summary>
/// <param name="InviteId">Davet kimliği.</param>
/// <param name="StudentId">Davetin ait olduğu öğrenci.</param>
/// <param name="ChildDisplayName">Velinin göreceği çocuk adı.</param>
/// <param name="InviterUserId">P15 / N-20: Daveti üreten kullanıcı (kendi kodunu kullanamaz).</param>
/// <param name="ExpiresOnUtc">P15 / N-20: Davetin son geçerlilik anı.</param>
public sealed record ParentInviteInfo(Guid InviteId, Guid StudentId, string? ChildDisplayName, Guid InviterUserId, DateTime ExpiresOnUtc);

/// <summary>
/// Students uygular; Parents tüketir. Veli, öğretmenin ürettiği kodu girerek çocuğuna bağlanır.
/// <para>
/// <b>A-05 — neden "işaretle" değil "tüket/geri ver":</b> Bağ Parents şemasında, davet Students
/// şemasında yaşar; ikisi ayrı <c>DbContext</c>'tir ve aralarında kapsayıcı bir transaction yoktur.
/// Eski akış önce bağı kaydedip <b>sonra</b> daveti işaretliyordu: ikinci yazım düşerse bağ kurulmuş
/// ama davet <c>Pending</c> kalmış oluyor, <b>aynı davet kodu ikinci bir veli tarafından tekrar
/// kullanılabiliyordu</b>. Ayrıca "önce oku, sonra işaretle" iki eşzamanlı isteğin aynı kodu
/// tüketmesine de açıktı.
/// </para>
/// <para>
/// Yeni akış: davet <b>önce</b> koşullu ve atomik biçimde tüketilir
/// (<see cref="TryConsumeAsync"/>); bağ kurulamazsa <see cref="ReleaseAsync"/> ile geri verilir
/// (telafi). Böylece kod tek kullanımlıktır ve başarısız bir akış daveti yakmaz.
/// </para>
/// </summary>
public interface IParentInviteDirectory
{
    /// <summary>
    /// Kodu çözümler (büyük/küçük harf duyarsız); kod yoksa ya da kullanılmışsa <c>null</c>.
    /// Süresi dolmuş davet <b>döner</b> — süre kararı ve hata mesajı tüketicinindir.
    /// </summary>
    /// <param name="inviteCode">Davet kodu.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Davet bilgisi ya da <c>null</c>.</returns>
    Task<ParentInviteInfo?> ResolveAsync(string inviteCode, CancellationToken cancellationToken);

    /// <summary>
    /// Daveti atomik olarak tüketir. Yalnız <c>Pending</c> durumundaki bir davet tüketilebilir;
    /// yarışı kaybeden ikinci çağrı <c>false</c> alır.
    /// </summary>
    /// <param name="inviteId">Davet kimliği.</param>
    /// <param name="parentUserId">Daveti kullanan veli.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Tüketildiyse <c>true</c>.</returns>
    Task<bool> TryConsumeAsync(Guid inviteId, Guid parentUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Telafi: tüketilmiş bir daveti yeniden kullanılabilir hâle getirir. Bağ kurulamadığında
    /// çağrılır — aksi hâlde veli, kendi hatası olmayan bir nedenle kodunu kaybederdi.
    /// </summary>
    /// <param name="inviteId">Davet kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>İşlem görevi.</returns>
    Task ReleaseAsync(Guid inviteId, CancellationToken cancellationToken);
}
