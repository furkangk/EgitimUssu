using EgitimUssu.Modules.Reporting.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class MonthlyOccurrenceCancellationTests
{
    [Fact]
    public void Different_weeks_count_separately_and_same_occurrence_is_idempotent()
    {
        var projection = new TeacherMonthlyProjection(Guid.NewGuid(), 2026, 11);
        var series = Guid.NewGuid();
        var first = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        projection.TrackCancelledOccurrence(series, first);
        projection.TrackCancelledOccurrence(series, first);
        projection.TrackCancelledOccurrence(series, first.AddDays(7));
        Assert.Equal(2, projection.CancelledLessonCount);
    }
}
