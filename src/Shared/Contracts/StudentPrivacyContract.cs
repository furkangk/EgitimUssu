namespace EgitimUssu.Shared.Contracts;

// Modüller-arası salt-okunur gizlilik sözleşmesi. Settings uygular; Parents (ve ileride başka modüller)
// öğrencinin bireysel çalışma verisini paylaşıp paylaşmadığını okumak için tüketir.
public sealed record StudentPrivacy(bool ShareStudyDataWithParent, bool ShareStudyDataWithTeacher);

public interface IStudentPrivacyDirectory
{
    // userId: öğrencinin login kullanıcı kimliği. Ayar kaydı yoksa paylaşım AÇIK varsayılır.
    Task<StudentPrivacy> GetForUserAsync(Guid userId, CancellationToken cancellationToken);
}
