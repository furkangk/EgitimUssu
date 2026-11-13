namespace EgitimUssu.Shared.Contracts;

/// <summary>
/// K-13: Öğretmenin bir öğrenci üzerindeki erişim kapsamı. Öğretmen tipi hesapta değil <b>ilişkide</b> durur;
/// aynı öğretmen bir öğrenciye özel ders verirken diğerine sınıf öğretmeni olabilir.
/// </summary>
public enum TeacherAccessScope
{
    /// <summary>Erişim yok — varsayılan-deny.</summary>
    None = 0,

    /// <summary>Yaşayan özel ders bağı: ders, ödeme, oturum, veli daveti, mesaj ve gelişim verisi.</summary>
    Private = 1,

    /// <summary>
    /// Sınıf üyeliği (P22, M19). <b>P22'ye kadar hiç dönmez.</b> Özel ders yetkisi vermez (K-17, K-18):
    /// yazma uçları bu yüzden <c>!= None</c> değil <c>== Private</c> karşılaştırır.
    /// </summary>
    Class = 2
}

/// <summary>
/// P15 T2 (N-08, N-21): Öğretmen–öğrenci erişim kapsamının <b>tek kaynağı</b>. Students uygular; öğrenciye
/// kayıt yazan her uç (ders, ödeme, oturum, veli daveti) bu cevaba dayanır.
/// <see cref="IStudentDirectory.IsLinkedTeacherOfStudentAsync"/> geriye uyumluluk için kalır ve
/// <c>GetScopeAsync == Private</c>'a delege eder; yeni kod doğrudan bunu kullanır.
/// </summary>
public interface ITeacherStudentAccess
{
    /// <summary>
    /// Öğretmenin öğrenci üzerindeki kapsamı.
    /// <para>
    /// <see cref="TeacherAccessScope.Private"/> ⇔ bağ <c>Linked</c> ve arşivli değil <b>veya</b> profil
    /// <c>TeacherManaged</c>, bu öğretmen tarafından açılmış ve öğretmenin bağı yaşıyor
    /// (<c>Manual</c>/<c>InviteSent</c>, arşivli değil). Arşivli, reddedilmiş, kopmuş bağ ya da hiç bağ yoksa
    /// <see cref="TeacherAccessScope.None"/>.
    /// </para>
    /// </summary>
    /// <param name="teacherUserId">Öğretmen kullanıcı kimliği.</param>
    /// <param name="studentId">Öğrenci profili kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Erişim kapsamı.</returns>
    Task<TeacherAccessScope> GetScopeAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken);
}
