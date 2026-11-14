using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P16 T1 / K-19: <see cref="ILocalCalendar"/> zamanın tek kaynağıdır. Sınır anları planın
/// Global Constraints'inden: TR 00:30 (= UTC 21:30 önceki gün), TR 02:59, TR 03:00,
/// ayın son günü 23:30 TR, ayın 1'i 01:00 TR.
/// </summary>
public sealed class LocalCalendarTests
{
    private static readonly DateTime Now = Utc(2026, 11, 13, 21, 30);

    private static LocalCalendar Istanbul(DateTime? now = null) =>
        new(LocalCalendarGuard.DefaultTimeZoneId, new FakeClock(now ?? Now));

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static void AssertUtc(DateTime expected, DateTime actual)
    {
        Assert.Equal(expected, actual);
        Assert.Equal(DateTimeKind.Utc, actual.Kind);
    }

    [Fact]
    public void Zone_Is_Europe_Istanbul_By_Default()
    {
        Assert.Equal("Europe/Istanbul", LocalCalendarGuard.DefaultTimeZoneId);
        Assert.Equal(TimeSpan.FromHours(3), Istanbul().Zone.GetUtcOffset(Now));
    }

    [Fact]
    public void ToLocalDate_Uses_Turkish_Date_Not_Utc_Date()
    {
        var calendar = Istanbul();

        // TR 00:30 — UTC tarihi hâlâ 13 Kasım.
        Assert.Equal(new DateOnly(2026, 11, 14), calendar.ToLocalDate(Utc(2026, 11, 13, 21, 30)));
        // TR 02:59 — UTC tarihi hâlâ 13 Kasım.
        Assert.Equal(new DateOnly(2026, 11, 14), calendar.ToLocalDate(Utc(2026, 11, 13, 23, 59)));
        // TR 03:00 — UTC ve TR aynı güne geçer.
        Assert.Equal(new DateOnly(2026, 11, 14), calendar.ToLocalDate(Utc(2026, 11, 14, 0, 0)));
        // TR 23:59 — hâlâ 13 Kasım.
        Assert.Equal(new DateOnly(2026, 11, 13), calendar.ToLocalDate(Utc(2026, 11, 13, 20, 59)));
    }

    [Fact]
    public void ToLocal_Converts_Utc_To_Turkish_Wall_Clock()
    {
        var local = Istanbul().ToLocal(Utc(2026, 11, 13, 21, 30));

        Assert.Equal(new DateTime(2026, 11, 14, 0, 30, 0), local);
        Assert.NotEqual(DateTimeKind.Utc, local.Kind);
    }

    [Fact]
    public void Unspecified_Kind_Is_Treated_As_Utc()
    {
        // EF InMemory ve bazı sürücüler Kind'ı Unspecified döndürür; değer UTC'dir.
        var unspecified = new DateTime(2026, 11, 13, 21, 30, 0, DateTimeKind.Unspecified);

        Assert.Equal(new DateOnly(2026, 11, 14), Istanbul().ToLocalDate(unspecified));
    }

    [Fact]
    public void Today_Is_The_Local_Date_Of_The_Clock()
    {
        Assert.Equal(new DateOnly(2026, 11, 14), Istanbul(Utc(2026, 11, 13, 21, 30)).Today());
        Assert.Equal(new DateOnly(2026, 11, 13), Istanbul(Utc(2026, 11, 13, 20, 59)).Today());
    }

    [Fact]
    public void ToUtc_Converts_Local_Wall_Clock_To_Utc()
    {
        AssertUtc(Utc(2026, 11, 14, 7, 0), Istanbul().ToUtc(new DateOnly(2026, 11, 14), new TimeOnly(10, 0)));
        AssertUtc(Utc(2026, 11, 13, 21, 0), Istanbul().ToUtc(new DateOnly(2026, 11, 14), TimeOnly.MinValue));
    }

    [Fact]
    public void DayRangeUtc_Spans_Local_Midnight_To_Next_Local_Midnight()
    {
        var (start, end) = Istanbul().DayRangeUtc(new DateOnly(2026, 11, 14));

        AssertUtc(Utc(2026, 11, 13, 21, 0), start);
        AssertUtc(Utc(2026, 11, 14, 21, 0), end);
    }

    [Theory]
    [InlineData(2026, 11, 9)]   // Pazartesi
    [InlineData(2026, 11, 14)]  // Cumartesi
    [InlineData(2026, 11, 15)]  // Pazar — haftanın son günü, sonraki haftaya taşmaz
    public void WeekRangeUtc_Starts_On_Monday_Local_Midnight(int year, int month, int day)
    {
        var (start, end) = Istanbul().WeekRangeUtc(new DateOnly(year, month, day));

        AssertUtc(Utc(2026, 11, 8, 21, 0), start);   // Pzt 9 Kasım 00:00 TR
        AssertUtc(Utc(2026, 11, 15, 21, 0), end);    // Pzt 16 Kasım 00:00 TR
    }

    [Fact]
    public void MonthRangeUtc_Spans_First_Local_Midnight_To_Next_Months()
    {
        var (start, end) = Istanbul().MonthRangeUtc(2026, 12);

        AssertUtc(Utc(2026, 11, 30, 21, 0), start);
        AssertUtc(Utc(2026, 12, 31, 21, 0), end);
    }

    [Fact]
    public void Month_Boundaries_Follow_Turkish_Time()
    {
        var calendar = Istanbul();
        var (start, end) = calendar.MonthRangeUtc(2026, 12);

        // Ayın 1'i 01:00 TR (= 30 Kasım 22:00 UTC) Aralık'tadır.
        var firstOfMonth = Utc(2026, 11, 30, 22, 0);
        Assert.True(firstOfMonth >= start && firstOfMonth < end);
        Assert.Equal(new DateOnly(2026, 12, 1), calendar.ToLocalDate(firstOfMonth));

        // Ayın son günü 23:30 TR (= 31 Aralık 20:30 UTC) hâlâ Aralık'tadır.
        var lastDayLate = Utc(2026, 12, 31, 20, 30);
        Assert.True(lastDayLate >= start && lastDayLate < end);
        Assert.Equal(new DateOnly(2026, 12, 31), calendar.ToLocalDate(lastDayLate));
    }

    [Theory]
    [InlineData(2026, 11, 13, 7, 0, "13 Kasım Cuma 10:00")]
    [InlineData(2026, 11, 13, 21, 30, "14 Kasım Cumartesi 00:30")]
    [InlineData(2026, 2, 1, 20, 59, "1 Şubat Pazar 23:59")]
    [InlineData(2026, 8, 17, 6, 5, "17 Ağustos Pazartesi 09:05")]
    public void FormatForUser_Prints_Turkish_Local_Time(int year, int month, int day, int hour, int minute, string expected)
    {
        Assert.Equal(expected, Istanbul().FormatForUser(Utc(year, month, day, hour, minute)));
    }

    [Fact]
    public void ToUtc_Skips_Forward_Over_A_Daylight_Saving_Gap()
    {
        // Yapılandırılabilir dilim DST'li olabilir; gece yarısı ileri sarılan bir günde gün başı
        // geçersiz yerel saattir. ConvertTimeToUtc orada fırlatır — takvim fırlatmamalı.
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2000, 1, 1),
            new DateTime(2099, 12, 31),
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 3, 29),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 10, 25));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test/MidnightDst", TimeSpan.Zero, "Test", "Test", "Test DST", [rule]);
        var calendar = new LocalCalendar(zone, new FakeClock(Now));

        var (start, end) = calendar.DayRangeUtc(new DateOnly(2026, 3, 29));

        // 29 Mart 00:00 yerelde yok; gün 01:00 DST'de (= 00:00 UTC) başlar, 23 saat sürer.
        AssertUtc(Utc(2026, 3, 29, 0, 0), start);
        AssertUtc(Utc(2026, 3, 29, 23, 0), end);
    }

    [Theory]
    [InlineData("Europe/Istanbul")]
    [InlineData("UTC")]
    public void Guard_Accepts_Resolvable_Time_Zone(string timeZoneId)
    {
        Assert.Null(LocalCalendarGuard.Validate(timeZoneId));
        Assert.NotNull(LocalCalendarGuard.Resolve(timeZoneId));
    }

    [Theory]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Guard_Rejects_Unresolvable_Time_Zone(string? timeZoneId)
    {
        var error = LocalCalendarGuard.Validate(timeZoneId);

        Assert.NotNull(error);
        Assert.Contains("Localization:TimeZone", error);
        var exception = Assert.Throws<InvalidOperationException>(() => LocalCalendarGuard.Resolve(timeZoneId));
        Assert.Equal(error, exception.Message);
    }
}
