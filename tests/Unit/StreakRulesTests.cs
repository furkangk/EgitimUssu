using EgitimUssu.Modules.Study.Application;

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
    public void StreakDate_RollsAt0400Local()
    {
        // 2026-07-20 00:30 Europe/Istanbul (UTC 2026-07-19 21:30) → hâlâ 19 Temmuz (04:00 öncesi)
        var utc = new DateTime(2026, 7, 19, 21, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 7, 19), StudyLocalTime.StreakDate(utc));

        // 2026-07-20 05:00 Europe/Istanbul (UTC 2026-07-20 02:00) → 20 Temmuz (04:00 sonrası)
        var utc2 = new DateTime(2026, 7, 20, 2, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 7, 20), StudyLocalTime.StreakDate(utc2));
    }
}
