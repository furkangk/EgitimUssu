using System.Globalization;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

/// <summary>
/// Tekrar kuralından üretilmiş somut bir ders oluşumu (occurrence).
/// <see cref="OriginalStartAtUtc"/> yalnızca ertelenmiş (Rescheduled) oluşumlarda doludur; istisna
/// kayıtlarının anahtarı için <see cref="RecurrenceIdUtc"/> kullanılır.
/// </summary>
public readonly record struct ScheduleOccurrence(
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    bool IsCancelled = false,
    DateTime? OriginalStartAtUtc = null)
{
    /// <summary>iCal RECURRENCE-ID: bu oluşumu hedefleyen istisna kaydının anahtarı.</summary>
    public DateTime RecurrenceIdUtc => OriginalStartAtUtc ?? StartAtUtc;
}

/// <summary>
/// Tekrar serisindeki tek bir oluşuma uygulanan istisna (iCal RECURRENCE-ID/EXDATE deseni).
/// <see cref="OriginalStartAtUtc"/> hedef oluşumu tanımlar; <see cref="Action"/> atlama/iptal/erteleme.
/// </summary>
public readonly record struct OccurrenceOverride(
    DateTime OriginalStartAtUtc,
    OccurrenceExceptionAction Action,
    DateTime? OverrideStartAtUtc,
    DateTime? OverrideEndAtUtc);

/// <summary>
/// iCal benzeri <c>RecurrenceRule</c> (FREQ=DAILY/WEEKLY/MONTHLY, BYDAY, UNTIL, COUNT) + ilk oluşumu alıp,
/// verilen [rangeStart, rangeEnd] penceresine (oluşum başlangıcı, iki uç dahil) düşen somut oluşumları üretir.
/// Kural boşsa tek oluşum döner.
/// <para>
/// P16 T2 / N-04 (K-19): genişletme serinin <b>yerel</b> takviminde yapılır — çapa serinin diliminde duvar
/// saatine çevrilir, gün/ay yerel takvimde ilerler, her oluşum UTC'ye geri çevrilir. Böylece <c>BYDAY</c>
/// yerel haftanın gününe eşlenir (Salı 01:30 TR = Pazartesi 22:30Z yine Salı'dır) ve DST'li dilimde duvar
/// saati korunur. Aylık kural ay gününü <b>çapadan</b> sabitler: kısa ayda o ayın son gününe kırpar ama
/// sonraki aya taşımaz (31 Ocak → 28 Şubat → 31 Mart).
/// </para>
/// <para>
/// <c>UNTIL</c>: <c>Z</c> sonekli değer UTC andır (mobil biçimi); Z'siz tarih-saat (RFC 5545 "floating")
/// serinin yerel saatidir; yalnız tarih (<c>yyyyMMdd</c>) o yerel günün tamamını kapsar.
/// <c>COUNT</c> seri başlangıcından itibaren yerel oluşumları sayar.
/// </para>
/// </summary>
public static class RecurrenceExpander
{
    private const int MaxDayIterations = 2000; // ~5.5 yıl güvenlik sınırı
    private const int MaxMonthIterations = 240; // 20 yıl

    /// <summary>Seriyi pencereye genişletir (istisnasız).</summary>
    /// <param name="anchorStartUtc">Serinin ilk oluşumunun başlangıcı (UTC).</param>
    /// <param name="anchorEndUtc">İlk oluşumun bitişi (UTC); süre bütün oluşumlara uygulanır.</param>
    /// <param name="recurrenceRule">iCal benzeri kural; boşsa tek oluşum.</param>
    /// <param name="zone">Serinin dilimi; çağıran <see cref="SeriesTimeZone.SeriesZone"/> ile çözer.</param>
    /// <param name="rangeStartUtc">Pencere başı (dahil).</param>
    /// <param name="rangeEndUtc">Pencere sonu (dahil).</param>
    public static IEnumerable<ScheduleOccurrence> Expand(
        DateTime anchorStartUtc,
        DateTime anchorEndUtc,
        string? recurrenceRule,
        TimeZoneInfo zone,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return ExpandCore(anchorStartUtc, anchorEndUtc, recurrenceRule, zone, rangeStartUtc, rangeEndUtc);
    }

    private static IEnumerable<ScheduleOccurrence> ExpandCore(
        DateTime anchorStartUtc,
        DateTime anchorEndUtc,
        string? recurrenceRule,
        TimeZoneInfo zone,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc)
    {
        var duration = anchorEndUtc - anchorStartUtc;
        if (duration <= TimeSpan.Zero)
        {
            duration = TimeSpan.FromHours(1);
        }

        if (string.IsNullOrWhiteSpace(recurrenceRule))
        {
            if (anchorStartUtc <= rangeEndUtc && anchorEndUtc >= rangeStartUtc)
            {
                yield return new ScheduleOccurrence(anchorStartUtc, anchorEndUtc);
            }

            yield break;
        }

        var rule = ParseRule(recurrenceRule);
        var untilUtc = rule.Until?.ToUtc(zone);
        var hardEnd = untilUtc.HasValue && untilUtc.Value < rangeEndUtc ? untilUtc.Value : rangeEndUtc;
        var anchorLocal = ToWallClock(anchorStartUtc, zone);

        // COUNT seri başlangıcından itibaren sayılır; pencerenin dışında kalan oluşumlar da sayıya dahildir.
        // Bu yüzden hardEnd'e kadar ilerlerken üretilen oluşum sayısı ayrıca izlenir.
        var produced = 0;

        foreach (var localStart in LocalStarts(rule, anchorLocal))
        {
            var startUtc = ToUtc(localStart, zone);
            if (startUtc > hardEnd)
            {
                break;
            }

            if (rule.Count.HasValue && produced >= rule.Count.Value)
            {
                break;
            }

            produced++;
            if (startUtc >= rangeStartUtc)
            {
                yield return new ScheduleOccurrence(startUtc, startUtc + duration);
            }
        }
    }

    /// <summary>
    /// Serinin yerel takvimindeki aday başlangıçlar (duvar saati), artan sırada. Adımlar en az bir gün
    /// olduğu için UTC karşılıkları da artan sıradadır.
    /// </summary>
    private static IEnumerable<DateTime> LocalStarts(ParsedRule rule, DateTime anchorLocal)
    {
        switch (rule.Frequency)
        {
            case RecurrenceFrequency.Daily:
                for (var day = 0; day < MaxDayIterations; day++)
                {
                    yield return anchorLocal.AddDays(day);
                }

                break;

            case RecurrenceFrequency.Weekly:
                var days = rule.ByDay.Count > 0
                    ? rule.ByDay
                    : new HashSet<DayOfWeek> { anchorLocal.DayOfWeek };
                for (var day = 0; day < MaxDayIterations; day++)
                {
                    var local = anchorLocal.AddDays(day);
                    if (days.Contains(local.DayOfWeek))
                    {
                        yield return local;
                    }
                }

                break;

            case RecurrenceFrequency.Monthly:
                // Her ay çapadan hesaplanır (birikimli değil): AddMonths kısa ayda son güne kırpar,
                // bir sonraki ay yine çapanın gününe döner.
                for (var month = 0; month < MaxMonthIterations; month++)
                {
                    yield return anchorLocal.AddMonths(month);
                }

                break;
        }
    }

    /// <summary>UTC anın serinin dilimindeki duvar saati (<see cref="DateTimeKind.Unspecified"/>).</summary>
    private static DateTime ToWallClock(DateTime utc, TimeZoneInfo zone)
    {
        // EF InMemory ve bazı okuma yolları Kind'ı Unspecified döndürür; saklanan her an UTC'dir.
        var asUtc = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        };

        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(asUtc, zone), DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Serinin dilimindeki duvar saatini UTC ana çevirir. DST boşluğundaki (var olmayan) saat boşluktan önceki
    /// ofsetle çevrilir ve boşluk kadar ileri kayar — <c>LocalCalendar.ToUtc</c> ile aynı kural
    /// (<see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/> orada fırlatırdı). Belirsiz saatte
    /// (geri sarma) standart ofset seçilir.
    /// </summary>
    private static DateTime ToUtc(DateTime localWallClock, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(localWallClock, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            var offsetBeforeGap = zone.GetUtcOffset(local.AddDays(-1));
            return DateTime.SpecifyKind(local - offsetBeforeGap, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    /// <summary>
    /// Tekrar oluşumlarını üretirken occurrence istisnalarını uygular: Skipped atlanır,
    /// Cancelled iptal işaretiyle döner (takvimde soluk), Rescheduled override tarih/saatle döner.
    /// </summary>
    public static IEnumerable<ScheduleOccurrence> Expand(
        DateTime anchorStartUtc,
        DateTime anchorEndUtc,
        string? recurrenceRule,
        TimeZoneInfo zone,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        IReadOnlyCollection<OccurrenceOverride> exceptions)
    {
        var byOriginal = exceptions.ToDictionary(e => e.OriginalStartAtUtc);

        foreach (var occurrence in Expand(anchorStartUtc, anchorEndUtc, recurrenceRule, zone, rangeStartUtc, rangeEndUtc))
        {
            if (!byOriginal.TryGetValue(occurrence.StartAtUtc, out var ex))
            {
                yield return occurrence;
                continue;
            }

            switch (ex.Action)
            {
                case OccurrenceExceptionAction.Skipped:
                    continue;
                case OccurrenceExceptionAction.Cancelled:
                    yield return occurrence with { IsCancelled = true };
                    break;
                case OccurrenceExceptionAction.Rescheduled:
                    var start = ex.OverrideStartAtUtc ?? occurrence.StartAtUtc;
                    var end = ex.OverrideEndAtUtc ?? occurrence.EndAtUtc;
                    if (start <= rangeEndUtc && end >= rangeStartUtc)
                    {
                        yield return new ScheduleOccurrence(start, end, false, occurrence.StartAtUtc);
                    }

                    break;
            }
        }
    }

    private static ParsedRule ParseRule(string rule)
    {
        var frequency = RecurrenceFrequency.Weekly;
        var byDay = new HashSet<DayOfWeek>();
        UntilBound? until = null;
        int? count = null;

        foreach (var part in rule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2)
            {
                continue;
            }

            var key = pair[0].Trim().ToUpperInvariant();
            var value = pair[1].Trim();

            switch (key)
            {
                case "FREQ":
                    frequency = value.ToUpperInvariant() switch
                    {
                        "DAILY" => RecurrenceFrequency.Daily,
                        "MONTHLY" => RecurrenceFrequency.Monthly,
                        _ => RecurrenceFrequency.Weekly
                    };
                    break;

                case "BYDAY":
                    foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var day = MapDay(token);
                        if (day.HasValue)
                        {
                            byDay.Add(day.Value);
                        }
                    }

                    break;

                case "UNTIL":
                    until = ParseUntil(value);
                    break;

                case "COUNT":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCount) && parsedCount > 0)
                    {
                        count = parsedCount;
                    }

                    break;
            }
        }

        return new ParsedRule(frequency, byDay, until, count);
    }

    private static DayOfWeek? MapDay(string token) => token.ToUpperInvariant() switch
    {
        "MO" => DayOfWeek.Monday,
        "TU" => DayOfWeek.Tuesday,
        "WE" => DayOfWeek.Wednesday,
        "TH" => DayOfWeek.Thursday,
        "FR" => DayOfWeek.Friday,
        "SA" => DayOfWeek.Saturday,
        "SU" => DayOfWeek.Sunday,
        _ => null
    };

    private static UntilBound? ParseUntil(string value)
    {
        const DateTimeStyles AsUtc = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, AsUtc, out var utc))
        {
            return new UntilBound(DateTime.SpecifyKind(utc, DateTimeKind.Utc), UntilKind.Utc);
        }

        // RFC 5545 "floating": Z'siz tarih-saat serinin yerel saatidir.
        if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var floating))
        {
            return new UntilBound(DateTime.SpecifyKind(floating, DateTimeKind.Unspecified), UntilKind.LocalDateTime);
        }

        if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new UntilBound(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), UntilKind.LocalDate);
        }

        // Son çare (ör. ISO 8601 "2026-07-26T23:59:00Z"): önceki davranış gibi UTC kabul edilir.
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, AsUtc, out var fallback)
            ? new UntilBound(DateTime.SpecifyKind(fallback, DateTimeKind.Utc), UntilKind.Utc)
            : null;
    }

    private enum RecurrenceFrequency
    {
        Daily,
        Weekly,
        Monthly
    }

    private enum UntilKind
    {
        /// <summary><c>Z</c> sonekli: UTC an.</summary>
        Utc,

        /// <summary>Z'siz tarih-saat: serinin yerel duvar saati.</summary>
        LocalDateTime,

        /// <summary>Yalnız tarih: o yerel günün tamamı dahil.</summary>
        LocalDate
    }

    private readonly record struct UntilBound(DateTime Value, UntilKind Kind)
    {
        /// <summary>Sınırın UTC karşılığı (dahil): oluşum başlangıcı bu ana eşit ya da önceyse üretilir.</summary>
        public DateTime ToUtc(TimeZoneInfo zone) => Kind switch
        {
            UntilKind.LocalDateTime => RecurrenceExpander.ToUtc(Value, zone),
            UntilKind.LocalDate => RecurrenceExpander.ToUtc(Value.AddDays(1), zone).AddTicks(-1),
            _ => Value,
        };
    }

    private sealed record ParsedRule(RecurrenceFrequency Frequency, HashSet<DayOfWeek> ByDay, UntilBound? Until, int? Count);
}

/// <summary>
/// P16 T2 / K-19: Serinin saklanan <c>TimeZone</c> kimliğini genişletme dilimine çevirir. Kimlik boşsa
/// <b>veya çözülemiyorsa</b> platformun yerel takvimine (<see cref="ILocalCalendar.Zone"/>) düşülür: doğrulayıcı
/// yalnız boş olmamayı ister, geçersiz bir kimlik takvim okumasını 500'e çevirmemeli.
/// </summary>
public static class SeriesTimeZone
{
    /// <summary>Seri dilimi; boş/çözülemeyen kimlikte <paramref name="calendar"/>'ın dilimi.</summary>
    public static TimeZoneInfo SeriesZone(this ILocalCalendar calendar, string? seriesTimeZone)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return Resolve(seriesTimeZone, calendar.Zone);
    }

    /// <summary>Kimliği çözer; boş/çözülemeyen kimlikte <paramref name="fallback"/> döner.</summary>
    public static TimeZoneInfo Resolve(string? timeZoneId, TimeZoneInfo fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return fallback;
        }

        // .NET çözülen sistem dilimlerini kendi içinde önbellekler; burada ayrıca önbellek tutulmaz
        // (geçersiz kimlikleri önbelleklemek kullanıcı girdisiyle sınırsız büyürdü).
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return fallback;
        }
    }
}
