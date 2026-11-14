using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Shared.Infrastructure;

namespace EgitimUssu.Tests.Unit;

public sealed class StreakRulesTests
{
    [Theory]
    [InlineData(120, 60, 72)]  // 120 dk hedef, %60 → 72 dk eşik
    [InlineData(0, 60, 20)]    // hedef yok → 20 dk sabit
    [InlineData(100, 65, 65)]  // %65
    public void EffectiveThresholdMinutes_Computes(int dailyGoal, int pct, int expected)
        => Assert.Equal(expected, StreakRules.EffectiveThresholdMinutes(dailyGoal, pct));

    [Fact]
    public void DayCounts_TrueWhenAtOrAboveThreshold_FalseBelow()
    {
        Assert.True(StreakRules.DayCounts(72, 120, 60));   // eşiğe eşit
        Assert.True(StreakRules.DayCounts(90, 120, 60));   // üstünde
        Assert.False(StreakRules.DayCounts(71, 120, 60));  // altında
        Assert.False(StreakRules.DayCounts(10, 0, 60));    // 10<20 sabit
        Assert.True(StreakRules.DayCounts(20, 0, 60));     // 20=20 sabit
    }

    [Fact]
    public void StudyDay_RollsAt0400Local()
    {
        // P16 T1: kural ILocalCalendar üzerinden; sabit UTC+3 ile aynı sonuç (Türkiye DST uygulamaz).
        var calendar = new LocalCalendar("Europe/Istanbul", new FakeClock(new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc)));

        // 2026-07-20 00:30 Europe/Istanbul (UTC 2026-07-19 21:30) → hâlâ 19 Temmuz (04:00 öncesi)
        var utc = new DateTime(2026, 7, 19, 21, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 7, 19), calendar.StudyDay(utc));

        // 2026-07-20 03:59 Europe/Istanbul (UTC 2026-07-20 00:59) → hâlâ 19 Temmuz
        Assert.Equal(new DateOnly(2026, 7, 19), calendar.StudyDay(new DateTime(2026, 7, 20, 0, 59, 0, DateTimeKind.Utc)));

        // 2026-07-20 04:00 Europe/Istanbul (UTC 2026-07-20 01:00) → 20 Temmuz (sınır dahil)
        Assert.Equal(new DateOnly(2026, 7, 20), calendar.StudyDay(new DateTime(2026, 7, 20, 1, 0, 0, DateTimeKind.Utc)));

        // 2026-07-20 05:00 Europe/Istanbul (UTC 2026-07-20 02:00) → 20 Temmuz (04:00 sonrası)
        var utc2 = new DateTime(2026, 7, 20, 2, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 7, 20), calendar.StudyDay(utc2));
    }
}
