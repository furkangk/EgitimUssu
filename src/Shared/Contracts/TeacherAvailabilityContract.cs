namespace EgitimUssu.Shared.Contracts;

// M14-5: Teachers uygular; Reporting (boş zaman analizi) tüketir. Modüller birbirinin DbContext'ini
// okumaz — haftalık uygunluk şablonu bu sözleşmeyle geçer (K-04: Shared/Contracts okuma arayüzü).

/// <summary>
/// Öğretmenin haftalık uygunluk şablonundan tek bir dilim. Saatler <b>UTC</b> duvar saati kabul edilir
/// (repo genelinde tüm hesaplar UTC; bkz. master tasarım §4). Tekrar eden bir şablondur: verilen
/// tarih aralığındaki <b>her</b> eşleşen güne uygulanır.
/// </summary>
/// <param name="DayOfWeek">Haftanın günü.</param>
/// <param name="StartTime">Başlangıç saati (UTC).</param>
/// <param name="EndTime">Bitiş saati (UTC).</param>
public sealed record TeacherWeeklySlot(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public interface ITeacherAvailabilityDirectory
{
    /// <summary>
    /// Öğretmenin haftalık uygunluk dilimleri. Profil yoksa boş liste döner — çağıran taraf
    /// "uygunluk tanımlanmamış" ile "hiç boş yok" arasındaki farkı kendi yorumlar.
    /// </summary>
    /// <param name="teacherUserId">Öğretmen kullanıcı kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Haftalık dilimler (boş olabilir).</returns>
    Task<IReadOnlyCollection<TeacherWeeklySlot>> ListWeeklySlotsAsync(Guid teacherUserId, CancellationToken cancellationToken);
}
