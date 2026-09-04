namespace EgitimUssu.Shared.Contracts;

// M14-4: Teachers uygular; Reporting (PDF rapor başlığı) tüketir. Uygunluk sözleşmesinden ayrıdır
// (ITeacherAvailabilityDirectory takvim verisi taşır) — kimlik/ad okuması kendi sözleşmesinde durur.

public interface ITeacherDirectory
{
    /// <summary>
    /// Öğretmenin görünen adı. Profil yoksa <c>null</c> döner; çağıran taraf bunu "öğretmen değil"
    /// ya da "profil açılmamış" olarak yorumlar ve uydurma ad üretmez.
    /// </summary>
    /// <param name="teacherUserId">Öğretmen kullanıcı kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Öğretmenin tam adı ya da <c>null</c>.</returns>
    Task<string?> GetDisplayNameAsync(Guid teacherUserId, CancellationToken cancellationToken);
}
