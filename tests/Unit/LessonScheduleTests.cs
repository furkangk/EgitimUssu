using EgitimUssu.Modules.Scheduling.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class LessonScheduleTests
{
    private static readonly DateTime Start = new(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 7, 20, 14, 0, 0, DateTimeKind.Utc);

    private static LessonSchedule NewLesson(string? meetingUrl = null, string? recurrenceRule = null)
        => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, Start, End, "Europe/Istanbul",
            recurrenceRule, LessonScheduleStatus.Planned, 60, "adres", meetingUrl, null, Start);

    [Fact]
    public void Ctor_StoresMeetingUrl()
    {
        var lesson = NewLesson(meetingUrl: "https://meet.example/abc");
        Assert.Equal("https://meet.example/abc", lesson.MeetingUrl);
    }

    [Fact]
    public void UpdateDetails_ChangesMeetingUrl()
    {
        var lesson = NewLesson(meetingUrl: "https://old");
        lesson.UpdateDetails("Matematik", ScheduledLessonFormat.Online, Start, End,
            "Europe/Istanbul", null, 60, "adres", "https://new", null, Start.AddMinutes(1));
        Assert.Equal("https://new", lesson.MeetingUrl);
    }
}
