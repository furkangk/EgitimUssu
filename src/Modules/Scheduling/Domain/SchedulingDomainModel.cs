using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Domain;

public sealed class LessonSchedule : AggregateRoot<Guid>
{
    private LessonSchedule()
    {
    }

    public LessonSchedule(
        Guid id,
        Guid teacherUserId,
        Guid studentId,
        string subject,
        ScheduledLessonFormat lessonFormat,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string timeZone,
        string? recurrenceRule,
        LessonScheduleStatus status,
        int reminderOffsetMinutes,
        string? locationLabel,
        string? notes,
        DateTime createdOnUtc)
    {
        Id = id;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        Subject = subject;
        LessonFormat = lessonFormat;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
        TimeZone = timeZone;
        RecurrenceRule = recurrenceRule;
        Status = status;
        ReminderOffsetMinutes = reminderOffsetMinutes;
        LocationLabel = locationLabel;
        Notes = notes;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;

        Raise(new LessonScheduledDomainEvent(Id, TeacherUserId, StudentId, StartAtUtc, EndAtUtc, createdOnUtc));
    }

    public Guid TeacherUserId { get; private set; }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public ScheduledLessonFormat LessonFormat { get; private set; }

    public DateTime StartAtUtc { get; private set; }

    public DateTime EndAtUtc { get; private set; }

    public string TimeZone { get; private set; } = string.Empty;

    public string? RecurrenceRule { get; private set; }

    public LessonScheduleStatus Status { get; private set; }

    public int ReminderOffsetMinutes { get; private set; }

    public string? LocationLabel { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public void Cancel(string? cancellationNote, DateTime updatedOnUtc)
    {
        Status = LessonScheduleStatus.Cancelled;
        UpdatedOnUtc = updatedOnUtc;

        if (!string.IsNullOrWhiteSpace(cancellationNote))
        {
            Notes = string.IsNullOrWhiteSpace(Notes)
                ? cancellationNote
                : $"{Notes}{Environment.NewLine}{cancellationNote}";
        }

        Raise(new LessonScheduleCancelledDomainEvent(Id, TeacherUserId, StudentId, updatedOnUtc));
    }

    public void Complete(DateTime updatedOnUtc)
    {
        Status = LessonScheduleStatus.Completed;
        UpdatedOnUtc = updatedOnUtc;

        Raise(new LessonSessionCompletedDomainEvent(Id, TeacherUserId, StudentId, updatedOnUtc));
    }
}

public enum ScheduledLessonFormat
{
    InPerson = 1,
    Online = 2,
    Hybrid = 3
}

public enum LessonScheduleStatus
{
    Draft = 1,
    Planned = 2,
    Cancelled = 3,
    Completed = 4
}

public sealed record LessonScheduledDomainEvent(
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    DateTime CreatedOnUtc) : DomainEvent;

public sealed record LessonScheduleCancelledDomainEvent(
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime CancelledOnUtc) : DomainEvent;

public sealed record LessonSessionCompletedDomainEvent(
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime CompletedOnUtc) : DomainEvent;
