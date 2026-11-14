using System.Globalization;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure;

/// <summary>
/// K-19: <see cref="ILocalCalendar"/>'ın <see cref="TimeZoneInfo"/> üzerine kurulu tek implementasyonu.
/// Saat aritmetiği yerel duvar saatinde yapılır, sonuç UTC'ye çevrilir — gün/hafta/ay sınırları
/// "24 saat ekle" ile değil bir sonraki yerel gece yarısından hesaplanır (dilim DST'li olabilir).
/// </summary>
public sealed class LocalCalendar : ILocalCalendar
{
    // Kültür verisine (ICU) bağlı kalmamak için sabit: Linux imajında tr-TR eksik olsa bile metin
    // sessizce İngilizceye dönmez. Sıra DayOfWeek'tir (Pazar = 0).
    private static readonly string[] MonthNames =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    private static readonly string[] DayNames =
        ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];

    private readonly IClock _clock;

    /// <summary>Çözülmüş zaman diliminden kurar.</summary>
    public LocalCalendar(TimeZoneInfo zone, IClock clock)
    {
        Zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>IANA kimliğinden kurar; çözülemezse <see cref="LocalCalendarGuard"/> fırlatır.</summary>
    public LocalCalendar(string timeZoneId, IClock clock)
        : this(LocalCalendarGuard.Resolve(timeZoneId), clock)
    {
    }

    /// <inheritdoc />
    public TimeZoneInfo Zone { get; }

    /// <inheritdoc />
    public DateOnly Today() => ToLocalDate(_clock.UtcNow);

    /// <inheritdoc />
    public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    /// <inheritdoc />
    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), Zone);

    /// <inheritdoc />
    public DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (Zone.IsInvalidTime(local))
        {
            // DST boşluğu (ör. 00:00 → 01:00 ileri sarma): saat boşluktan önceki ofsetle çevrilir,
            // sonuç boşluk kadar ileri kayar — gün başı ilk geçerli yerel ana (01:00) düşer.
            // ConvertTimeToUtc burada fırlatırdı.
            var offsetBeforeGap = Zone.GetUtcOffset(local.AddDays(-1));
            return DateTime.SpecifyKind(local - offsetBeforeGap, DateTimeKind.Utc);
        }

        // Belirsiz saatte (geri sarma) ConvertTimeToUtc standart ofseti seçer — deterministik.
        return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
    }

    /// <inheritdoc />
    public (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateOnly date) =>
        (ToUtc(date, TimeOnly.MinValue), ToUtc(date.AddDays(1), TimeOnly.MinValue));

    /// <inheritdoc />
    public (DateTime StartUtc, DateTime EndUtc) WeekRangeUtc(DateOnly anyDayInWeek)
    {
        var daysSinceMonday = ((int)anyDayInWeek.DayOfWeek + 6) % 7;
        var monday = anyDayInWeek.AddDays(-daysSinceMonday);
        return (ToUtc(monday, TimeOnly.MinValue), ToUtc(monday.AddDays(7), TimeOnly.MinValue));
    }

    /// <inheritdoc />
    public (DateTime StartUtc, DateTime EndUtc) MonthRangeUtc(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return (ToUtc(first, TimeOnly.MinValue), ToUtc(first.AddMonths(1), TimeOnly.MinValue));
    }

    /// <inheritdoc />
    public string FormatForUser(DateTime utc)
    {
        var local = ToLocal(utc);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{local.Day} {MonthNames[local.Month - 1]} {DayNames[(int)local.DayOfWeek]} {local:HH:mm}");
    }

    // EF InMemory ve bazı okuma yolları Kind'ı Unspecified döndürür; saklanan her an UTC'dir.
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
