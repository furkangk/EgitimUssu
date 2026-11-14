using System.Globalization;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>
/// P16 T5 / N-50, N-06 (metin): bildirim metinlerinde kullanıcıya gösterilen zamanın tek biçimleyicisi — her zaman
/// <b>Türkiye yerel saati</b> (<see cref="ILocalCalendar"/>, K-19). Eskiden UTC basılıyordu: 23:59 TR son teslim
/// "20:59", 1 Eylül 00:30 TR vade "31.08", ders hatırlatması ISO UTC damgası görünüyordu.
/// <para>
/// Yalnız metin. Hatırlatmanın <b>anı</b> (ne zaman gönderileceği) ve oluşum bazlı hatırlatma P17 T6'dadır.
/// </para>
/// </summary>
public static class NotificationTimeText
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary><c>dd.MM.yyyy HH:mm</c>, yerel — ödev son teslimi.</summary>
    public static string LocalDateTime(ILocalCalendar calendar, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return calendar.ToLocal(utc).ToString("dd.MM.yyyy HH:mm", Turkish);
    }

    /// <summary><c>dd.MM.yyyy</c>, yerel gün — ödeme vadesi.</summary>
    public static string LocalDate(ILocalCalendar calendar, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return calendar.ToLocalDate(utc).ToString("dd.MM.yyyy", Turkish);
    }

    /// <summary>Ders hatırlatma gövdesi: <c>"Ders başlangıcı: 13 Kasım Cuma 10:00."</c> (gün/ay adları sabit tablodan).</summary>
    public static string LessonReminderBody(ILocalCalendar calendar, DateTime startAtUtc)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return $"Ders başlangıcı: {calendar.FormatForUser(startAtUtc)}.";
    }
}
