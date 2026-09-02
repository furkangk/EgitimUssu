namespace EgitimUssu.Shared.Contracts;

// M15-4: Öğrencinin çalışma verisi gizlilik seviyesi (Settings'teki PrivacyLevel enum'unun
// modüller-arası karşılığı; Shared.Contracts, Settings.Domain'e bağımlı olamaz).
public enum StudyPrivacyLevel
{
    // Öğretmen ve onaylı veli çalışma özetini + detayını görür.
    Standard = 1,

    // Yalnız haftalık toplam süre ve ders katılımı görünür; seans/test detayı gizlenir.
    Limited = 2,

    // Hiçbir çalışma verisi paylaşılmaz (yalnız ders/ödev/ödeme).
    Hidden = 3
}

// Modüller-arası salt-okunur gizlilik sözleşmesi. Settings uygular; Parents (ve ileride başka modüller)
// öğrencinin bireysel çalışma verisini paylaşıp paylaşmadığını okumak için tüketir.
// Level varsayılanı Standard'dır: ayar kaydı olmayan öğrenci için mevcut davranış korunur.
public sealed record StudentPrivacy(
    bool ShareStudyDataWithParent,
    bool ShareStudyDataWithTeacher,
    StudyPrivacyLevel Level = StudyPrivacyLevel.Standard);

public interface IStudentPrivacyDirectory
{
    // userId: öğrencinin login kullanıcı kimliği. Ayar kaydı yoksa paylaşım AÇIK varsayılır.
    Task<StudentPrivacy> GetForUserAsync(Guid userId, CancellationToken cancellationToken);
}
