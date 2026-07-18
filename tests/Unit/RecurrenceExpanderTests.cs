using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// Öğrenci takviminin tekrar (recurrence) genişletmesini koruyan testler. Bu mantık takvimde
/// "her Pazartesi" gibi tekrarları güne yaymak ve öğretmen dersiyle çakışmayı hesaplamak için
/// kullanılır; hatalı olursa dersler yanlış günlerde görünür veya çakışma kaçırılır.
/// </summary>
public sealed class RecurrenceExpanderTests
{
    private static readonly DateTime Monday = new(2026, 7, 6, 15, 0, 0, DateTimeKind.Utc); // Pazartesi
    private static readonly DateTime MondayEnd = new(2026, 7, 6, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoRule_ReturnsSingleOccurrence_WhenInRange()
    {
        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, null, Monday.AddDays(-1), Monday.AddDays(7))
            .ToArray();

        Assert.Single(result);
        Assert.Equal(Monday, result[0].StartAtUtc);
        Assert.Equal(MondayEnd, result[0].EndAtUtc);
    }

    [Fact]
    public void NoRule_ReturnsEmpty_WhenOutsideRange()
    {
        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, null, Monday.AddDays(3), Monday.AddDays(7))
            .ToArray();

        Assert.Empty(result);
    }

    [Fact]
    public void WeeklyByDay_ExpandsToEachMatchingWeekday_UntilBound()
    {
        // Her Pazartesi ve Çarşamba, 3 hafta boyunca.
        const string rule = "FREQ=WEEKLY;BYDAY=MO,WE;UNTIL=20260726T235900Z";

        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, rule, Monday, Monday.AddDays(30))
            .ToArray();

        // 6,8,13,15,20,22 Temmuz → 6 oluşum (26'sı Pazar, dahil değil).
        Assert.Equal(6, result.Length);
        Assert.All(result, o => Assert.Contains(o.StartAtUtc.DayOfWeek, new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }));
        Assert.All(result, o => Assert.Equal(new TimeSpan(15, 0, 0), o.StartAtUtc.TimeOfDay));
    }

    [Fact]
    public void Daily_ExpandsEveryDayWithinRange()
    {
        const string rule = "FREQ=DAILY;UNTIL=20260710T235900Z";

        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, rule, Monday, Monday.AddDays(30))
            .ToArray();

        // 6,7,8,9,10 Temmuz → 5 gün.
        Assert.Equal(5, result.Length);
    }

    [Fact]
    public void Monthly_KeepsDayOfMonth()
    {
        const string rule = "FREQ=MONTHLY;UNTIL=20261006T235900Z";

        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, rule, Monday, Monday.AddDays(120))
            .ToArray();

        // 6 Temmuz, 6 Ağustos, 6 Eylül, 6 Ekim → 4 oluşum.
        Assert.Equal(4, result.Length);
        Assert.All(result, o => Assert.Equal(6, o.StartAtUtc.Day));
    }

    [Fact]
    public void RangeWindow_ClipsOccurrencesOutsideRequestedRange()
    {
        const string rule = "FREQ=WEEKLY;BYDAY=MO;UNTIL=20260831T235900Z";

        // Sadece 13-20 Temmuz penceresi istenirse yalnızca 13 ve 20 dönmeli.
        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, rule, new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 20, 23, 59, 0, DateTimeKind.Utc))
            .ToArray();

        Assert.Equal(2, result.Length);
        Assert.Equal(13, result[0].StartAtUtc.Day);
        Assert.Equal(20, result[1].StartAtUtc.Day);
    }

    [Fact]
    public void Expand_WithSkipException_OmitsThatOccurrence()
    {
        // Her Pazartesi, 3 hafta
        var rule = "FREQ=WEEKLY;BYDAY=MO";
        var rangeStart = Monday.AddDays(-1);
        var rangeEnd = Monday.AddDays(21);
        var secondMonday = Monday.AddDays(7);

        var exceptions = new[]
        {
            new OccurrenceOverride(secondMonday, OccurrenceExceptionAction.Skipped, null, null)
        };

        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, rule, rangeStart, rangeEnd, exceptions)
            .ToArray();

        Assert.DoesNotContain(result, o => o.StartAtUtc == secondMonday);
        Assert.Contains(result, o => o.StartAtUtc == Monday);
    }

    [Fact]
    public void Expand_WithRescheduleException_MovesOccurrence()
    {
        var rule = "FREQ=WEEKLY;BYDAY=MO";
        var secondMonday = Monday.AddDays(7);
        var moved = secondMonday.AddDays(2); // Çarşamba
        var exceptions = new[]
        {
            new OccurrenceOverride(secondMonday, OccurrenceExceptionAction.Rescheduled, moved, moved.AddHours(1))
        };

        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, rule, Monday.AddDays(-1), Monday.AddDays(21), exceptions)
            .ToArray();

        Assert.DoesNotContain(result, o => o.StartAtUtc == secondMonday);
        Assert.Contains(result, o => o.StartAtUtc == moved);
    }

    [Fact]
    public void Expand_WithCancelException_MarksCancelled()
    {
        var rule = "FREQ=WEEKLY;BYDAY=MO";
        var secondMonday = Monday.AddDays(7);
        var exceptions = new[]
        {
            new OccurrenceOverride(secondMonday, OccurrenceExceptionAction.Cancelled, null, null)
        };

        var result = RecurrenceExpander
            .Expand(Monday, MondayEnd, rule, Monday.AddDays(-1), Monday.AddDays(21), exceptions)
            .ToArray();

        Assert.Contains(result, o => o.StartAtUtc == secondMonday && o.IsCancelled);
    }
}
