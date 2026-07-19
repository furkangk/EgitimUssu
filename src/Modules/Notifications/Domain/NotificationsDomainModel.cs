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

    /// <summary>
    /// Hatırlatmayı yeni ders zamanına taşır ve tekrar bekleyen (Pending) duruma alır. Kaynak ders/girdi
    /// güncellendiğinde (reschedule) kullanılır; aynı satır korunur (tek satır kısıtı bozulmaz).
    /// </summary>
    public void Reschedule(DateTime scheduledLessonStartAtUtc, DateTime remindAtUtc, DateTime updatedOnUtc)
    {
        ScheduledLessonStartAtUtc = scheduledLessonStartAtUtc;
        RemindAtUtc = remindAtUtc;
        Status = ReminderStatus.Pending;
        UpdatedOnUtc = updatedOnUtc;
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

/// <summary>
/// Veliye üretilmiş bildirim (Veli V-E). Yalnız Premium veliye + ilgili tercih açıkken üretilir.
/// Olay bazlı (yeni ödev, ders tamamlandı, ödeme, bağlantı) veya haftalık özet.
/// </summary>
public sealed class ParentNotification : AggregateRoot<Guid>
{
    private ParentNotification()
    {
    }

    public ParentNotification(Guid id, Guid parentUserId, Guid studentId, ParentNotificationType type, string title, string message, DateTime createdOnUtc)
    {
        Id = id;
        ParentUserId = parentUserId;
        StudentId = studentId;
        Type = type;
        Title = title;
        Message = message;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid ParentUserId { get; private set; }

    public Guid StudentId { get; private set; }

    public ParentNotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public DateTime CreatedOnUtc { get; private set; }
}

public enum ParentNotificationType
{
    WeeklySummary = 1,
    NewAssignment = 2,
    LessonCompleted = 3,
    PaymentUpdate = 4,
    LinkConnected = 5,
    PaymentDeclared = 6
}

/// <summary>
/// İşlenmiş entegrasyon olayı idempotency anahtarı (Notifications; Parents deseni birebir).
/// Veli bildirim işleyicileri ve haftalık özet servisi çift-üretimi önlemek için kullanır.
/// </summary>
public sealed class ProcessedIntegrationEvent : Entity<Guid>
{
    private ProcessedIntegrationEvent()
    {
    }

    public ProcessedIntegrationEvent(Guid eventId, string eventName, DateTime processedOnUtc)
    {
        Id = eventId;
        EventName = eventName;
        ProcessedOnUtc = processedOnUtc;
    }

    public string EventName { get; private set; } = string.Empty;

    public DateTime ProcessedOnUtc { get; private set; }
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
