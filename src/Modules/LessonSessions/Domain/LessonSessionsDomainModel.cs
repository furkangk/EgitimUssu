using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.LessonSessions.Domain;

public sealed class LessonSession : AggregateRoot<Guid>
{
    private LessonSession()
    {
    }

    public LessonSession(
        Guid id,
        Guid? lessonScheduleId,
        Guid teacherUserId,
        Guid studentId,
        string subject,
        DateTime plannedStartAtUtc,
        DateTime? actualStartAtUtc,
        DateTime? actualEndAtUtc,
        int? durationMinutes,
        StudentAttendanceStatus attendanceStatus,
        LessonSessionStatus status,
        string topicTitle,
        string? coveredContent,
        string? teacherNotes,
        DateTime createdOnUtc,
        DateTime? completedOnUtc)
    {
        Id = id;
        LessonScheduleId = lessonScheduleId;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        Subject = subject;
        PlannedStartAtUtc = plannedStartAtUtc;
        ActualStartAtUtc = actualStartAtUtc;
        ActualEndAtUtc = actualEndAtUtc;
        DurationMinutes = durationMinutes;
        AttendanceStatus = attendanceStatus;
        Status = status;
        TopicTitle = topicTitle;
        CoveredContent = coveredContent;
        TeacherNotes = teacherNotes;
        CreatedOnUtc = createdOnUtc;
        CompletedOnUtc = completedOnUtc;

        Raise(new LessonSessionCreatedDomainEvent(Id, LessonScheduleId, TeacherUserId, StudentId, PlannedStartAtUtc, createdOnUtc));
    }

    public Guid? LessonScheduleId { get; private set; }

    public Guid TeacherUserId { get; private set; }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public DateTime PlannedStartAtUtc { get; private set; }

    public DateTime? ActualStartAtUtc { get; private set; }

    public DateTime? ActualEndAtUtc { get; private set; }

    public int? DurationMinutes { get; private set; }

    public StudentAttendanceStatus AttendanceStatus { get; private set; }

    public LessonSessionStatus Status { get; private set; }

    public string TopicTitle { get; private set; } = string.Empty;

    public string? CoveredContent { get; private set; }

    public string? TeacherNotes { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime? CompletedOnUtc { get; private set; }

    public void Complete(
        DateTime actualStartAtUtc,
        DateTime actualEndAtUtc,
        StudentAttendanceStatus attendanceStatus,
        string topicTitle,
        string? coveredContent,
        string? teacherNotes,
        DateTime completedOnUtc)
    {
        ActualStartAtUtc = actualStartAtUtc;
        ActualEndAtUtc = actualEndAtUtc;
        DurationMinutes = (int)Math.Ceiling((actualEndAtUtc - actualStartAtUtc).TotalMinutes);
        AttendanceStatus = attendanceStatus;
        Status = LessonSessionStatus.Completed;
        TopicTitle = topicTitle;
        CoveredContent = coveredContent;
        TeacherNotes = teacherNotes;
        CompletedOnUtc = completedOnUtc;

        Raise(new LessonSessionCompletedDomainEvent(Id, LessonScheduleId, TeacherUserId, StudentId, completedOnUtc));
    }
}

public enum StudentAttendanceStatus
{
    Unknown = 1,
    Attended = 2,
    Late = 3,
    Absent = 4
}

public enum LessonSessionStatus
{
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public sealed record LessonSessionCreatedDomainEvent(
    Guid LessonSessionId,
    Guid? LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime PlannedStartAtUtc,
    DateTime CreatedOnUtc) : DomainEvent;

public sealed record LessonSessionCompletedDomainEvent(
    Guid LessonSessionId,
    Guid? LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime CompletedOnUtc) : DomainEvent;
