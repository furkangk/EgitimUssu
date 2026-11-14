using EgitimUssu.Modules.LessonSessions.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class OccurrenceKeyedSessionTests
{
    private static readonly DateTime Start = new(2026, 11, 16, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Scheduled_session_keeps_original_key_when_planned_start_is_rescheduled()
    {
        var session = NewSession(Guid.NewGuid(), Start.AddHours(2), Start);
        Assert.Equal(Start, session.OccurrenceStartUtc);
        Assert.Equal(Start.AddHours(2), session.PlannedStartAtUtc);
    }

    [Fact]
    public void Ad_hoc_session_has_no_occurrence_key()
        => Assert.Null(NewSession(null, Start).OccurrenceStartUtc);

    [Fact]
    public void Recompletion_updates_notes_without_a_second_completion_event_or_timestamp()
    {
        var session = NewSession(Guid.NewGuid(), Start);
        session.ClearDomainEvents();
        Assert.True(session.Complete(Start, Start.AddHours(1), StudentAttendanceStatus.Attended,
            "Konu", null, "İlk not", false, Start.AddHours(1)).IsSuccess);
        Assert.Single(session.DomainEvents.OfType<LessonSessionCompletedDomainEvent>());
        session.ClearDomainEvents();
        Assert.True(session.Complete(Start, Start.AddHours(1), StudentAttendanceStatus.Attended,
            "Konu", null, "Düzeltilen not", false, Start.AddHours(2)).IsSuccess);
        Assert.Empty(session.DomainEvents);
        Assert.Equal("Düzeltilen not", session.TeacherNotes);
        Assert.Equal(Start.AddHours(1), session.CompletedOnUtc);
    }

    private static LessonSession NewSession(Guid? schedule, DateTime planned, DateTime? occurrence = null)
        => new(Guid.NewGuid(), schedule, Guid.NewGuid(), Guid.NewGuid(), "Matematik", planned,
            null, null, null, StudentAttendanceStatus.Unknown, LessonSessionStatus.Planned,
            "Konu", null, null, Start, null, occurrence);
}
