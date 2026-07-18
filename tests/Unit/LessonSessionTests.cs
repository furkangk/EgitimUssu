using EgitimUssu.Modules.LessonSessions.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class LessonSessionTests
{
    private static readonly DateTime Planned = new(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Complete_AbsentChargeable_IsRecorded()
    {
        var session = new LessonSession(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            Planned, null, null, null,
            StudentAttendanceStatus.Unknown, LessonSessionStatus.Planned, "Konu", null, null, Planned, null);

        session.Complete(Planned, Planned.AddHours(1), StudentAttendanceStatus.Absent,
            "Konu", null, "gelmedi", isChargeable: true, Planned.AddHours(1));

        Assert.Equal(LessonSessionStatus.Completed, session.Status);
        Assert.Equal(StudentAttendanceStatus.Absent, session.AttendanceStatus);
        Assert.True(session.IsChargeable);
    }
}
