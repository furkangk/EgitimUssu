namespace EgitimUssu.Shared.Contracts;

// Modüller-arası salt-okunur (read) sözleşme. Students bu sözleşmeyi uygular; diğer modüller
// (ör. Scheduling) öğrenci↔kullanıcı sahipliğini doğrulamak için tüketir. Böylece modüller
// birbirine doğrudan proje referansı vermez (anti-corruption / paylaşılan kontrat).
/// <summary>
/// B-07: Bir öğretmen↔öğrenci ikilisi için anlaşılan **ders başına** ücret
/// (<c>TeacherStudentLink.AgreedRateAmount</c>/<c>Currency</c>).
/// </summary>
/// <param name="Amount">Ders başına tutar.</param>
/// <param name="Currency">Para birimi (ör. "TRY").</param>
public sealed record AgreedLessonRate(decimal Amount, string Currency);

public interface IStudentDirectory
{
    /// <summary>
    /// E-07: Bir kullanıcıya ait <b>tüm</b> öğrenci profillerinin kimlikleri.
    /// <para>
    /// <c>GetOwnerUserIdAsync</c>'in tersidir. Hesap silme akışı buna muhtaçtır: Study, Assignments,
    /// LessonSessions ve Reporting kayıtlarını <c>StudentId</c> ile tutar ve modül sınırı yüzünden
    /// bunu <c>UserId</c>'den kendisi çözemez. Profili yoksa <b>boş liste</b> döner.
    /// </para>
    /// </summary>
    /// <param name="userId">Kullanıcı kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    Task<IReadOnlyCollection<Guid>> GetStudentIdsForUserAsync(Guid userId, CancellationToken cancellationToken);

    // Öğrenci profiline bağlı kullanıcının kimliği (Student.UserId).
    // Profil bulunamazsa veya bağlı kullanıcı yoksa null döner.
    Task<Guid?> GetOwnerUserIdAsync(Guid studentId, CancellationToken cancellationToken);

    /// <summary>
    /// M07-2: Ders ücretlendirmesi için öğrenci bazlı anlaşılan ücreti okur (B-07).
    /// Bağ yoksa ya da ücret hiç girilmemişse <c>null</c> döner — çağıran taraf
    /// tahmini bir tutar üretmez.
    /// <para>
    /// P15 T9 (K-18): ücret yalnız <b>yaşayan özel ders bağında</b> döner
    /// (<see cref="ITeacherStudentAccess.GetScopeAsync"/> <c>== Private</c>). Bağı arşivlenmiş/bitmiş ikilinin
    /// girilmiş ücreti de <c>null</c>'dır — otomatik ders ücreti oluşmaz. Kontrol çağrı anındadır: dersi tamamlayıp
    /// olay işlenmeden bağı arşivleyen öğretmenin o dersi için de ücret açılmaz (bilinçli; öğretmen bağı arşivden
    /// çıkarıp ücreti elle girer — manuel kayıt da yaşayan bağ ister).
    /// </para>
    /// </summary>
    /// <param name="teacherUserId">Öğretmen kullanıcı kimliği.</param>
    /// <param name="studentId">Öğrenci profili kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Anlaşılan ücret ya da <c>null</c>.</returns>
    Task<AgreedLessonRate?> GetAgreedLessonRateAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken);

    /// <summary>
    /// M10-2: Öğretmenin bu öğrenciyle <b>yaşayan</b> bir bağı var mı (<c>TeacherStudentLink</c>)?
    /// Reddedilmiş/koparılmış/arşivlenmiş bağ <c>false</c> döner — öğretmen görünümü yetkisi
    /// bu cevaba dayanır (varsayılan-deny).
    /// <para>
    /// P15 T2: geriye uyumluluk için kalır; <see cref="ITeacherStudentAccess.GetScopeAsync"/> <c>== Private</c>'a
    /// delege eder. Yeni kod doğrudan <see cref="ITeacherStudentAccess"/> kullanır.
    /// </para>
    /// </summary>
    /// <param name="teacherUserId">Öğretmen kullanıcı kimliği.</param>
    /// <param name="studentId">Öğrenci profili kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Bağ varsa <c>true</c>.</returns>
    Task<bool> IsLinkedTeacherOfStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken);

    /// <summary>
    /// M14-4: Rapor başlığında kullanılacak öğrenci adı. Profil yoksa <c>null</c> döner — çağıran taraf
    /// yerine bir yer tutucu yazar, uydurma ad üretmez.
    /// </summary>
    /// <param name="studentId">Öğrenci profili kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Öğrencinin tam adı ya da <c>null</c>.</returns>
    Task<string?> GetDisplayNameAsync(Guid studentId, CancellationToken cancellationToken);
}
