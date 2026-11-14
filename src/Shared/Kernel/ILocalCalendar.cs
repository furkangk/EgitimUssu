namespace EgitimUssu.Shared.Kernel;

/// <summary>
/// K-19: Zamanın tek kaynağı. Saklanan her an UTC'dir; "bugün", "bu hafta", "bu ay" ve kullanıcıya
/// gösterilen her saat ise yerel takvimdir (varsayılan <c>Europe/Istanbul</c>, yapılandırma
/// <c>Localization:TimeZone</c>). İş kuralında <c>DateOnly.FromDateTime(utc)</c>, <c>utc.Date</c> ve
/// UTC <c>DayOfWeek</c> yasaktır — Türkiye'de 00:00–02:59 arası UTC'de hâlâ önceki gündür.
/// <para>
/// Girdi olarak verilen UTC anlar <see cref="DateTimeKind.Unspecified"/> ise UTC sayılır; dönen UTC
/// anlar daima <see cref="DateTimeKind.Utc"/>'dir (Npgsql <c>timestamptz</c> başka tür kabul etmez).
/// </para>
/// </summary>
public interface ILocalCalendar
{
    /// <summary>Yerel zaman dilimi.</summary>
    TimeZoneInfo Zone { get; }

    /// <summary><see cref="IClock.UtcNow"/>'un yerel tarihi.</summary>
    DateOnly Today();

    /// <summary>UTC anın yerel tarihi.</summary>
    DateOnly ToLocalDate(DateTime utc);

    /// <summary>UTC anın yerel duvar saati (<see cref="DateTimeKind.Unspecified"/>).</summary>
    DateTime ToLocal(DateTime utc);

    /// <summary>Yerel tarih + saati UTC ana çevirir. Yerelde var olmayan saat (DST boşluğu) ileri kaydırılır.</summary>
    DateTime ToUtc(DateOnly date, TimeOnly time);

    /// <summary>Yerel günün UTC aralığı: <c>[gün 00:00, ertesi gün 00:00)</c>.</summary>
    (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateOnly date);

    /// <summary>Verilen günü içeren yerel haftanın UTC aralığı; hafta Pazartesi 00:00'da başlar.</summary>
    (DateTime StartUtc, DateTime EndUtc) WeekRangeUtc(DateOnly anyDayInWeek);

    /// <summary>Yerel ayın UTC aralığı: <c>[ayın 1'i 00:00, sonraki ayın 1'i 00:00)</c>.</summary>
    (DateTime StartUtc, DateTime EndUtc) MonthRangeUtc(int year, int month);

    /// <summary>Kullanıcıya gösterilecek yerel saat, ör. <c>"13 Kasım Cuma 10:00"</c>.</summary>
    string FormatForUser(DateTime utc);
}
