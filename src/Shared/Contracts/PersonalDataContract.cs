namespace EgitimUssu.Shared.Contracts;

/// <summary>
/// E-07: Silinecek kişinin, modüllerin kendi kayıtlarını bulmak için ihtiyaç duyduğu kimlikleri.
/// <para>
/// <b>Neden iki kimlik?</b> Bu kod tabanında öğretmen tarafı <c>UserId</c> (Identity), öğrenci tarafı
/// ise <c>StudentId</c> (<c>StudentProfile.Id</c>) ile referanslanır — modüller-arası bilinen asimetri
/// (<c>doc/modules/veri_modeli.md</c> §2). Study, Assignments, LessonSessions ve Reporting yalnız
/// <c>StudentId</c> bilir ve modül sınırı yüzünden onu <c>UserId</c>'den kendisi çözemez; bu yüzden
/// çözülmüş liste konuya iliştirilerek taşınır.
/// </para>
/// </summary>
/// <param name="UserId">Silinen kullanıcının Identity kimliği.</param>
/// <param name="StudentIds">Bu kullanıcıya ait öğrenci profillerinin kimlikleri (yoksa boş).</param>
public sealed record PersonalDataSubject(Guid UserId, IReadOnlyCollection<Guid> StudentIds);

/// <summary>
/// E-07: Bir modülün, silinen kullanıcıya ait kişisel verisini anonimleştirmesi.
/// <para>
/// Her modül <b>kendi</b> şemasından sorumludur; hesap silme akışı bu arayüzün tüm
/// implementasyonlarını çağırır. Yeni bir modül kişisel veri tutmaya başladığında buraya bir
/// implementasyon eklemek zorundadır — aksi halde silme talebi <b>sessizce eksik</b> kalır.
/// </para>
/// <para>
/// <b>İdempotent olmalıdır:</b> ikinci çağrıda hiçbir şey değiştirmemeli ve <c>0</c> döndürmelidir.
/// Anonimleştirme yarıda kalırsa akış yeniden koşar.
/// </para>
/// </summary>
public interface IPersonalDataAnonymizer
{
    /// <summary>Log ve tanılama için modül adı (ör. "Teachers").</summary>
    string ModuleName { get; }

    /// <summary>Bu modüldeki kişisel veriyi anonimleştirir/siler.</summary>
    /// <param name="subject">Silinen kişinin kimlikleri.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Etkilenen kayıt sayısı (değişiklik yoksa 0).</returns>
    Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken);
}
