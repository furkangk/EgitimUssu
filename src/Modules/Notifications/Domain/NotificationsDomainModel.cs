using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Domain;

public sealed class LessonReminder : AggregateRoot<Guid>
{
    /// <summary>Bu sayıya ulaşan hatırlatma kalıcı olarak <see cref="ReminderStatus.Failed"/> olur.</summary>
    public const int MaxDeliveryAttempts = 5;

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
        DateTime createdOnUtc,
        DateTime? occurrenceStartUtc = null,
        Guid? recipientUserId = null)
    {
        Id = id;
        LessonScheduleId = lessonScheduleId;
        OccurrenceStartUtc = occurrenceStartUtc ?? scheduledLessonStartAtUtc;
        RecipientUserId = recipientUserId ?? teacherUserId;
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

    public DateTime OccurrenceStartUtc { get; private set; }

    public Guid RecipientUserId { get; private set; }

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

    /// <summary>Başarısız push teslimat denemesi sayısı (P03 Task 3).</summary>
    public int AttemptCount { get; private set; }

    /// <summary>Son teslimat hatasının metni; tanılama için saklanır.</summary>
    public string? LastError { get; private set; }

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

    public void Expire(DateTime nowUtc)
    {
        if (Status != ReminderStatus.Pending) return;
        Status = ReminderStatus.Expired;
        UpdatedOnUtc = nowUtc;
    }

    public void Replan(DateTime startUtc, DateTime remindUtc, string message, DateTime nowUtc)
    {
        Reschedule(startUtc, remindUtc, nowUtc);
        Message = message;
        AttemptCount = 0;
        LastError = null;
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

    /// <summary>
    /// Push teslimatı başarısız oldu. Hatırlatma <b>Sent işaretlenmez</b> — bir sonraki turda tekrar denenir.
    /// <see cref="MaxDeliveryAttempts"/> denemeden sonra kalıcı olarak <see cref="ReminderStatus.Failed"/> olur,
    /// böylece kuyruk sonsuza kadar aynı satırı denemez.
    /// </summary>
    public void MarkFailed(string error, DateTime nowUtc)
    {
        if (Status != ReminderStatus.Pending)
        {
            return;
        }

        AttemptCount++;
        LastError = error;
        UpdatedOnUtc = nowUtc;

        if (AttemptCount >= MaxDeliveryAttempts)
        {
            Status = ReminderStatus.Failed;
        }
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
    Cancelled = 3,

    /// <summary>Maksimum deneme sayısına ulaşıldı; artık denenmez (P03 Task 3).</summary>
    Failed = 4,
    Expired = 5
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

public enum DevicePlatform
{
    Android = 1,
    IOS = 2,
    Web = 3
}

/// <summary>
/// Bir kullanicinin bir cihazindaki push token'i. Ayni token yeniden kaydedilirse yeni satir
/// acilmaz, mevcut satir <see cref="Touch"/> ile tazelenir (FCM token'lari rotasyona ugrar).
/// </summary>
public sealed class DeviceToken : AggregateRoot<Guid>
{
    private DeviceToken()
    {
    }

    public DeviceToken(
        Guid id,
        Guid userId,
        string token,
        DevicePlatform platform,
        string? deviceName,
        DateTime createdOnUtc)
    {
        Id = id;
        UserId = userId;
        Token = token;
        Platform = platform;
        DeviceName = deviceName;
        IsActive = true;
        CreatedOnUtc = createdOnUtc;
        LastSeenOnUtc = createdOnUtc;
    }

    public Guid UserId { get; private set; }

    public string Token { get; private set; } = string.Empty;

    public DevicePlatform Platform { get; private set; }

    public string? DeviceName { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime LastSeenOnUtc { get; private set; }

    public void Touch(DateTime nowUtc)
    {
        IsActive = true;
        LastSeenOnUtc = nowUtc;
    }

    /// <summary>FCM "UNREGISTERED"/"INVALID_ARGUMENT" dondurdugunde veya kullanici cikis yaptiginda cagrilir.</summary>
    public void Deactivate(DateTime nowUtc)
    {
        IsActive = false;
        LastSeenOnUtc = nowUtc;
    }
}

/// <summary>Rolden bağımsız in-app bildirim türü (M11-3).</summary>
public enum UserNotificationKind
{
    LessonReminder = 1,
    AssignmentCreated = 2,
    AssignmentDue = 3,
    AssignmentMissed = 4,
    PaymentDue = 5,
    PaymentOverdue = 6,
    LessonCompleted = 7,
    MessageReceived = 8,
    System = 9
}

/// <summary>
/// Rolden bağımsız in-app bildirim (M11-3/M11-4). Veli-özel <see cref="ParentNotification"/> korunur;
/// yeni akışlar bu genel tipi üretir — öğrenci, öğretmen ve veli aynı bildirim merkezini kullanır.
/// <see cref="PushedOnUtc"/> alanı, aynı kaydın <c>NotificationDispatcher</c> turunda bir kez push
/// edilmesini sağlar (çift bildirim yok).
/// </summary>
public sealed class UserNotification : AggregateRoot<Guid>
{
    private UserNotification()
    {
    }

    public UserNotification(
        Guid id,
        Guid userId,
        UserNotificationKind kind,
        string title,
        string body,
        string? route,
        DateTime createdOnUtc)
    {
        Id = id;
        UserId = userId;
        Kind = kind;
        Title = title;
        Body = body;
        Route = route;
        CreatedOnUtc = createdOnUtc;
        ExpiresOnUtc = createdOnUtc.AddMinutes(kind == UserNotificationKind.LessonReminder ? 30 : 1440);
    }

    public Guid UserId { get; private set; }

    public UserNotificationKind Kind { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>Mobilde dokunulunca gidilecek rota (ör. "/student/assignments").</summary>
    public string? Route { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime? ReadOnUtc { get; private set; }

    /// <summary>Push olarak gönderildiği an; <c>null</c> ise dispatcher turunda gönderilecek.</summary>
    public DateTime? PushedOnUtc { get; private set; }

    public int PushAttempts { get; private set; }
    public DateTime? NextAttemptOnUtc { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public NotificationPushStatus PushStatus { get; private set; }
    public void MarkPushFailed(DateTime nowUtc)
    {
        if (PushStatus != NotificationPushStatus.Pending || PushedOnUtc.HasValue) return;
        ExpirePush(nowUtc);
        if (PushStatus != NotificationPushStatus.Pending) return;
        PushAttempts++;
        if (PushAttempts >= 8)
        {
            PushStatus = NotificationPushStatus.Failed;
            NextAttemptOnUtc = null;
        }
        else NextAttemptOnUtc = nowUtc.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, PushAttempts - 1)));
    }
    public void ExpirePush(DateTime nowUtc)
    {
        if (PushStatus != NotificationPushStatus.Pending || PushedOnUtc.HasValue || nowUtc < ExpiresOnUtc) return;
        PushStatus = NotificationPushStatus.Expired;
        NextAttemptOnUtc = null;
    }

    public bool IsRead => ReadOnUtc.HasValue;

    /// <summary>Okundu işareti idempotenttir; ilk okuma zamanı korunur.</summary>
    public void MarkRead(DateTime nowUtc) => ReadOnUtc ??= nowUtc;

    /// <summary>Push gönderimi idempotenttir; aynı bildirim iki kez push edilmez.</summary>
    public void MarkPushed(DateTime nowUtc)
    {
        if (PushStatus != NotificationPushStatus.Pending) return;
        PushedOnUtc ??= nowUtc;
        PushStatus = NotificationPushStatus.Sent;
        NextAttemptOnUtc = null;
    }
}

public enum NotificationPushStatus { Pending = 0, Sent = 1, Expired = 2, Failed = 3 }
