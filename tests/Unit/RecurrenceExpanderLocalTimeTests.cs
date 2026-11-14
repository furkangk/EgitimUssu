using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P16 T2 / N-04: Tekrar kuralı serinin <b>yerel</b> takviminde genişletilir. Türkiye'de 00:00–02:59 arası
/// UTC'de hâlâ önceki gündür; "Salı 01:30" dersi UTC'de Pazartesi 22:30'dur. Genişletme UTC gününe
/// bakarsa <c>BYDAY=TU</c> ilk dersi üretmez, sonrakileri Çarşamba'ya düşürür. Aylık kural ay gününü
/// çapadan sabitler (31 Ocak → 28 Şubat → 31 Mart), birikimli <c>AddMonths</c> ile kaymaz.
/// </summary>
public sealed class RecurrenceExpanderLocalTimeTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    // Salı 17 Kasım 2026 01:30 TR = Pazartesi 16 Kasım 22:30Z.
    private static readonly DateTime TuesdayNightUtc = new(2026, 11, 16, 22, 30, 0, DateTimeKind.Utc);

    private static DateTime LocalOf(DateTime utc, TimeZoneInfo zone) => TimeZoneInfo.ConvertTimeFromUtc(utc, zone);

    private static ScheduleOccurrence[] Expand(
        DateTime anchorUtc, string? rule, TimeZoneInfo zone, DateTime rangeStartUtc, DateTime rangeEndUtc)
        => RecurrenceExpander.Expand(anchorUtc, anchorUtc.AddHours(1), rule, zone, rangeStartUtc, rangeEndUtc).ToArray();

    [Fact]
    public void Weekly_byday_matches_local_weekday_and_produces_first_occurrence()
    {
        var result = Expand(TuesdayNightUtc, "FREQ=WEEKLY;BYDAY=TU", Istanbul, TuesdayNightUtc, TuesdayNightUtc.AddDays(28));

        // 17, 24 Kasım, 1, 8, 15 Aralık — pencere sonu (15 Aralık 01:30 TR) dahil.
        Assert.Equal(5, result.Length);
        Assert.Equal(TuesdayNightUtc, result[0].StartAtUtc);
        Assert.All(result, occurrence =>
        {
            var local = LocalOf(occurrence.StartAtUtc, Istanbul);
            Assert.Equal(DayOfWeek.Tuesday, local.DayOfWeek);
            Assert.Equal(new TimeSpan(1, 30, 0), local.TimeOfDay);
            Assert.Equal(DateTimeKind.Utc, occurrence.StartAtUtc.Kind);
            Assert.Equal(occurrence.StartAtUtc.AddHours(1), occurrence.EndAtUtc);
        });
    }

    [Fact]
    public void Weekly_without_byday_repeats_on_anchor_local_weekday()
    {
        var result = Expand(TuesdayNightUtc, "FREQ=WEEKLY", Istanbul, TuesdayNightUtc, TuesdayNightUtc.AddDays(14));

        Assert.Equal(3, result.Length);
        Assert.All(result, occurrence => Assert.Equal(DayOfWeek.Tuesday, LocalOf(occurrence.StartAtUtc, Istanbul).DayOfWeek));
    }

    [Fact]
    public void Weekly_count_counts_local_occurrences()
    {
        var result = Expand(TuesdayNightUtc, "FREQ=WEEKLY;BYDAY=TU;COUNT=3", Istanbul, TuesdayNightUtc, TuesdayNightUtc.AddDays(60));

        Assert.Equal(
            new[] { TuesdayNightUtc, TuesdayNightUtc.AddDays(7), TuesdayNightUtc.AddDays(14) },
            result.Select(occurrence => occurrence.StartAtUtc));
    }

    [Fact]
    public void Floating_until_is_read_in_series_local_time()
    {
        // UNTIL Z'siz (RFC 5545 "floating") → serinin yerel saati: Pazartesi 30 Kasım 23:59 TR = 20:59Z.
        // 1 Aralık 01:30 TR (= 30 Kasım 22:30Z) yerelde sınırın ötesinde kalır.
        var result = Expand(TuesdayNightUtc, "FREQ=WEEKLY;UNTIL=20261130T235900", Istanbul, TuesdayNightUtc, TuesdayNightUtc.AddDays(60));

        Assert.Equal(new[] { TuesdayNightUtc, TuesdayNightUtc.AddDays(7) }, result.Select(occurrence => occurrence.StartAtUtc));
    }

    [Fact]
    public void Utc_until_stays_an_instant()
    {
        // Mobil biçimi: yerel bitiş günü 23:59 → UTC an (30 Kasım 23:59 TR = 20:59Z).
        var result = Expand(TuesdayNightUtc, "FREQ=WEEKLY;BYDAY=TU;UNTIL=20261130T205900Z", Istanbul, TuesdayNightUtc, TuesdayNightUtc.AddDays(60));

        Assert.Equal(new[] { TuesdayNightUtc, TuesdayNightUtc.AddDays(7) }, result.Select(occurrence => occurrence.StartAtUtc));
    }

    [Fact]
    public void Date_only_until_includes_that_whole_local_day()
    {
        // Salı 17 Kasım 18:00 TR = 15:00Z. UNTIL=20261124 → 24 Kasım'ın (yerel) tamamı dahil.
        var anchor = new DateTime(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc);

        var result = Expand(anchor, "FREQ=WEEKLY;UNTIL=20261124", Istanbul, anchor, anchor.AddDays(60));

        Assert.Equal(new[] { anchor, anchor.AddDays(7) }, result.Select(occurrence => occurrence.StartAtUtc));
    }

    [Fact]
    public void Monthly_pins_day_of_month_to_anchor_and_clips_short_months()
    {
        // 31 Ocak 2027 10:00 TR = 07:00Z.
        var anchor = new DateTime(2027, 1, 31, 7, 0, 0, DateTimeKind.Utc);

        var result = Expand(anchor, "FREQ=MONTHLY", Istanbul, anchor, new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(
            new[]
            {
                new DateTime(2027, 1, 31, 10, 0, 0),
                new DateTime(2027, 2, 28, 10, 0, 0),
                new DateTime(2027, 3, 31, 10, 0, 0),
                new DateTime(2027, 4, 30, 10, 0, 0),
                new DateTime(2027, 5, 31, 10, 0, 0),
            },
            result.Select(occurrence => LocalOf(occurrence.StartAtUtc, Istanbul)));
    }

    [Fact]
    public void Monthly_on_the_first_at_0100_local_stays_on_the_first()
    {
        // 1 Aralık 2026 01:00 TR = 30 Kasım 22:00Z — UTC'de ayın 30'u.
        var anchor = new DateTime(2026, 11, 30, 22, 0, 0, DateTimeKind.Utc);

        var result = Expand(anchor, "FREQ=MONTHLY;COUNT=6", Istanbul, anchor, anchor.AddDays(220));

        Assert.Equal(6, result.Length);
        Assert.All(result, occurrence =>
        {
            var local = LocalOf(occurrence.StartAtUtc, Istanbul);
            Assert.Equal(1, local.Day);
            Assert.Equal(new TimeSpan(1, 0, 0), local.TimeOfDay);
        });
    }

    [Fact]
    public void Series_zone_with_dst_keeps_local_wall_time_across_the_change()
    {
        // Berlin: 25 Ekim 2026'da CEST (+2) → CET (+1). Salı 20 Ekim 18:00 CEST = 16:00Z.
        var anchor = new DateTime(2026, 10, 20, 16, 0, 0, DateTimeKind.Utc);

        var result = Expand(anchor, "FREQ=WEEKLY;COUNT=3", Berlin, anchor, anchor.AddDays(30));

        Assert.Equal(3, result.Length);
        Assert.All(result, occurrence => Assert.Equal(new TimeSpan(18, 0, 0), LocalOf(occurrence.StartAtUtc, Berlin).TimeOfDay));
        Assert.Equal(new DateTime(2026, 10, 27, 17, 0, 0, DateTimeKind.Utc), result[1].StartAtUtc);
    }

    [Fact]
    public void Exceptions_target_local_occurrence_instants()
    {
        var skipped = TuesdayNightUtc.AddDays(7);
        var exceptions = new[] { new OccurrenceOverride(skipped, OccurrenceExceptionAction.Skipped, null, null) };

        var result = RecurrenceExpander
            .Expand(TuesdayNightUtc, TuesdayNightUtc.AddHours(1), "FREQ=WEEKLY;BYDAY=TU", Istanbul,
                TuesdayNightUtc, TuesdayNightUtc.AddDays(14), exceptions)
            .ToArray();

        Assert.Equal(new[] { TuesdayNightUtc, TuesdayNightUtc.AddDays(14) }, result.Select(occurrence => occurrence.StartAtUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not/A_Zone")]
    [InlineData("Europe")] // Linux'ta zoneinfo altında klasör → SecurityException; Windows'ta bulunamadı
    [InlineData("../../etc/passwd")]
    public void Empty_or_unresolvable_series_zone_falls_back_to_platform_zone(string? seriesTimeZone)
    {
        Assert.Same(Istanbul, SeriesTimeZone.Resolve(seriesTimeZone, Istanbul));
    }

    [Fact]
    public void Resolvable_series_zone_wins_over_platform_zone()
    {
        var zone = SeriesTimeZone.Resolve(" Europe/Berlin ", Istanbul);

        Assert.True(zone.HasSameRules(Berlin));
    }
}
