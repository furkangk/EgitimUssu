using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Domain;

public sealed class LessonReminder : AggregateRoot<Guid>
{
    private LessonReminder()
    {
    }

    public LessonReminder(
        Guid id,
        Guid lessonScheduleId,
        Guid teacherUserId,
        Guid studentId,
        string title,
        string message,
        DateTime scheduledLessonStartAtUtc,
        DateTime remindAtUtc,
        NotificationChannel channel,
        ReminderStatus status,
        DateTime createdOnUtc)
    {
        Id = id;
        LessonScheduleId = lessonScheduleId;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        Title = title;
        Message = message;
        ScheduledLessonStartAtUtc = scheduledLessonStartAtUtc;
        RemindAtUtc = remindAtUtc;
        Channel = channel;
        Status = status;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;

        Raise(new LessonReminderCreatedDomainEvent(Id, LessonScheduleId, TeacherUserId, StudentId, RemindAtUtc, CreatedOnUtc));
    }

    public Guid LessonScheduleId { get; private set; }

    public Guid TeacherUserId { get; private set; }

    public Guid StudentId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public DateTime ScheduledLessonStartAtUtc { get; private set; }

    public DateTime RemindAtUtc { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public ReminderStatus Status { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public void Cancel(DateTime updatedOnUtc)
    {
        if (Status == ReminderStatus.Cancelled)
        {
            return;
        }

        Status = ReminderStatus.Cancelled;
        UpdatedOnUtc = updatedOnUtc;

        Raise(new LessonReminderCancelledDomainEvent(Id, LessonScheduleId, TeacherUserId, StudentId, updatedOnUtc));
    }

    public void MarkSent(DateTime updatedOnUtc)
    {
        if (Status != ReminderStatus.Pending)
        {
            return;
        }

        Status = ReminderStatus.Sent;
        UpdatedOnUtc = updatedOnUtc;

        Raise(new LessonReminderSentDomainEvent(Id, LessonScheduleId, TeacherUserId, StudentId, updatedOnUtc));
    }
}

public enum NotificationChannel
{
    InApp = 1,
    Push = 2
}

public enum ReminderStatus
{
    Pending = 1,
    Sent = 2,
    Cancelled = 3
}

public sealed record LessonReminderCreatedDomainEvent(
    Guid LessonReminderId,
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime RemindAtUtc,
    DateTime CreatedOnUtc) : DomainEvent;

public sealed record LessonReminderCancelledDomainEvent(
    Guid LessonReminderId,
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime CancelledOnUtc) : DomainEvent;

public sealed record LessonReminderSentDomainEvent(
    Guid LessonReminderId,
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime SentOnUtc) : DomainEvent;
