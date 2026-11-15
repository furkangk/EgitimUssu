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
/// <param name="IsRecurring">Tekrarlı seri mi.</param>
/// <param name="Status">Planlı dersin durumu (<c>Draft</c>/<c>Planned</c>/<c>Cancelled</c>/<c>Completed</c>).</param>
public sealed record LessonScheduleDetails(
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Subject,
    string? MeetingUrl,
    string Status,
    bool IsRecurring = false);

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

    /// <summary>Orijinal oluşum anahtarını doğrular, ertelenmiş görünen saatini döndürür.</summary>
    Task<LessonScheduleOccurrence?> GetOccurrenceAsync(Guid lessonScheduleId, DateTime occurrenceStartUtc, CancellationToken cancellationToken);

    /// <summary>
    /// M14-5: Öğretmenin verilen aralıkta <b>dolu</b> olduğu zaman parçaları — planlı ders oluşumları
    /// (tekrar kuralları açılmış, occurrence istisnaları uygulanmış) ve izin/tatil blokları.
    /// Boş zaman analizi bu listeyi haftalık uygunluk şablonundan çıkarır.
    /// </summary>
    /// <param name="teacherUserId">Öğretmen kullanıcı kimliği.</param>
    /// <param name="fromUtc">Aralık başlangıcı (UTC, dahil).</param>
    /// <param name="toUtc">Aralık bitişi (UTC, hariç).</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Dolu aralıklar (sıralı olması garanti değildir).</returns>
    Task<IReadOnlyCollection<TeacherBusyInterval>> ListTeacherBusyIntervalsAsync(
        Guid teacherUserId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);
}

/// <summary>Öğretmenin dolu olduğu tek bir zaman aralığı.</summary>
/// <param name="StartAtUtc">Başlangıç (UTC).</param>
/// <param name="EndAtUtc">Bitiş (UTC).</param>
/// <param name="Kind">Doluluğun kaynağı: <c>Lesson</c> veya <c>TimeOff</c>.</param>
public sealed record TeacherBusyInterval(DateTime StartAtUtc, DateTime EndAtUtc, string Kind);

public sealed record LessonScheduleOccurrence(DateTime OccurrenceStartUtc, DateTime StartAtUtc, DateTime EndAtUtc, bool IsCancelled);

/// <summary>Bildirim planlayıcısı için görünen pencereye genişletilmiş, aktif oluşumlar.</summary>
public interface ILessonScheduleReader
{
    Task<IReadOnlyCollection<ReminderOccurrence>> ListReminderOccurrencesAsync(
        DateTime fromUtc, DateTime toUtc, Guid? lessonScheduleId, CancellationToken cancellationToken);
}

public sealed record ReminderOccurrence(Guid LessonScheduleId, Guid? TeacherUserId, Guid StudentId,
    DateTime OccurrenceStartUtc, DateTime StartAtUtc, int ReminderOffsetMinutes);

/// <summary>Öğretmen panosu için güncel kaynaktan, görünür başlangıç penceresindeki oluşumlar.</summary>
public interface ITeacherDashboardLessonSource
{
    Task<IReadOnlyCollection<DashboardLessonOccurrence>> ListDashboardOccurrencesAsync(
        DateTime fromUtc, DateTime toUtc, Guid? lessonScheduleId, CancellationToken cancellationToken);
}

public sealed record DashboardLessonOccurrence(Guid TeacherUserId, Guid LessonScheduleId,
    DateTime OccurrenceStartUtc, DateTime StartAtUtc, bool IsCancelled);
