namespace EgitimUssu.Shared.Contracts;

// M05-2: Scheduling'in dışa açtığı salt-okunur sözleşme; LessonSessions tüketir.
// Modüller birbirine proje referansı vermez (Y2 deseni — bkz. LessonSessionReadContract).

/// <summary>Planlı dersin (M04) oturum türetmek için gereken alanları.</summary>
/// <param name="LessonScheduleId">Planlı ders kimliği.</param>
/// <param name="TeacherUserId">Dersi veren öğretmen.</param>
/// <param name="StudentId">Öğrenci.</param>
/// <param name="StartAtUtc">Başlangıç (UTC).</param>
/// <param name="EndAtUtc">Bitiş (UTC).</param>
/// <param name="Subject">Ders/branş.</param>
/// <param name="MeetingUrl">Online ders bağlantısı (varsa).</param>
/// <param name="Status">Planlı dersin durumu (<c>Draft</c>/<c>Planned</c>/<c>Cancelled</c>/<c>Completed</c>).</param>
public sealed record LessonScheduleDetails(
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Subject,
    string? MeetingUrl,
    string Status);

/// <summary>Scheduling'in dışa açtığı okuma sözleşmesi (LessonSessions tüketir).</summary>
public interface ILessonScheduleDirectory
{
    /// <summary>
    /// Planlı dersi kimliğiyle döner. Öğrencinin **kendi** dersi (öğretmensiz, Ç-06 self ders)
    /// bu sözleşmeden <c>null</c> döner — oturum türetilemez.
    /// </summary>
    /// <param name="lessonScheduleId">Planlı ders kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Ders ayrıntısı ya da <c>null</c>.</returns>
    Task<LessonScheduleDetails?> GetAsync(Guid lessonScheduleId, CancellationToken cancellationToken);
}
