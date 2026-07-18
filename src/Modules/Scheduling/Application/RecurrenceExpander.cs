using System.Globalization;
using EgitimUssu.Modules.Scheduling.Domain;

namespace EgitimUssu.Modules.Scheduling.Application;

/// <summary>Tekrar kuralından üretilmiş somut bir ders oluşumu (occurrence).</summary>
public readonly record struct ScheduleOccurrence(DateTime StartAtUtc, DateTime EndAtUtc, bool IsCancelled = false);

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
/// iCal benzeri <c>RecurrenceRule</c> (FREQ=DAILY/WEEKLY/MONTHLY, BYDAY, UNTIL) + ilk oluşumu alıp,
/// verilen [rangeStart, rangeEnd] penceresine düşen somut oluşumları üretir. Kural boşsa tek oluşum döner.
///
/// Not: Zaman aritmetiği UTC instant üzerinden yapılır. Türkiye (Europe/Istanbul) DST uygulamadığı için
/// yerel duvar-saati korunur; DST'li bölgeler için ileride yerel-saat tabanlı genişletmeye geçilebilir.
/// </summary>
public static class RecurrenceExpander
{
    private const int MaxDayIterations = 2000; // ~5.5 yıl güvenlik sınırı
    private const int MaxMonthIterations = 240; // 20 yıl

    public static IEnumerable<ScheduleOccurrence> Expand(
        DateTime anchorStartUtc,
        DateTime anchorEndUtc,
        string? recurrenceRule,
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
        var hardEnd = rule.Until.HasValue && rule.Until.Value < rangeEndUtc ? rule.Until.Value : rangeEndUtc;

        switch (rule.Frequency)
        {
            case RecurrenceFrequency.Daily:
            {
                var cursor = anchorStartUtc;
                var guard = 0;
                while (cursor <= hardEnd && guard++ < MaxDayIterations)
                {
                    if (cursor >= rangeStartUtc)
                    {
                        yield return new ScheduleOccurrence(cursor, cursor + duration);
                    }

                    cursor = cursor.AddDays(1);
                }

                break;
            }

            case RecurrenceFrequency.Weekly:
            {
                var days = rule.ByDay.Count > 0
                    ? rule.ByDay
                    : new HashSet<DayOfWeek> { anchorStartUtc.DayOfWeek };
                var cursor = anchorStartUtc;
                var guard = 0;
                while (cursor <= hardEnd && guard++ < MaxDayIterations)
                {
                    if (cursor >= rangeStartUtc && days.Contains(cursor.DayOfWeek))
                    {
                        yield return new ScheduleOccurrence(cursor, cursor + duration);
                    }

                    cursor = cursor.AddDays(1);
                }

                break;
            }

            case RecurrenceFrequency.Monthly:
            {
                var cursor = anchorStartUtc;
                var guard = 0;
                while (cursor <= hardEnd && guard++ < MaxMonthIterations)
                {
                    if (cursor >= rangeStartUtc)
                    {
                        yield return new ScheduleOccurrence(cursor, cursor + duration);
                    }

                    cursor = cursor.AddMonths(1);
                }

                break;
            }
        }
    }

    /// <summary>
    /// Tekrar oluşumlarını üretirken occurrence istisnalarını uygular: Skipped atlanır,
    /// Cancelled iptal işaretiyle döner (takvimde soluk), Rescheduled override tarih/saatle döner.
    /// </summary>
    public static IEnumerable<ScheduleOccurrence> Expand(
        DateTime anchorStartUtc,
        DateTime anchorEndUtc,
        string? recurrenceRule,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        IReadOnlyCollection<OccurrenceOverride> exceptions)
    {
        var byOriginal = exceptions.ToDictionary(e => e.OriginalStartAtUtc);

        foreach (var occurrence in Expand(anchorStartUtc, anchorEndUtc, recurrenceRule, rangeStartUtc, rangeEndUtc))
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
                        yield return new ScheduleOccurrence(start, end);
                    }

                    break;
            }
        }
    }

    private static ParsedRule ParseRule(string rule)
    {
        var frequency = RecurrenceFrequency.Weekly;
        var byDay = new HashSet<DayOfWeek>();
        DateTime? until = null;

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
            }
        }

        return new ParsedRule(frequency, byDay, until);
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

    private static DateTime? ParseUntil(string value)
    {
        var formats = new[]
        {
            "yyyyMMdd'T'HHmmss'Z'",
            "yyyyMMdd'T'HHmmss",
            "yyyyMMdd"
        };

        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fallback)
            ? DateTime.SpecifyKind(fallback, DateTimeKind.Utc)
            : null;
    }

    private enum RecurrenceFrequency
    {
        Daily,
        Weekly,
        Monthly
    }

    private sealed record ParsedRule(RecurrenceFrequency Frequency, HashSet<DayOfWeek> ByDay, DateTime? Until);
}
