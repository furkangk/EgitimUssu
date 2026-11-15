using System.Diagnostics;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Observability;
using EgitimUssu.Shared.Infrastructure.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

public interface INotificationDispatchProcessor
{
    Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default);

    /// <summary>M11-3: Henüz push edilmemiş in-app bildirimleri cihazlara gönderir.</summary>
    Task<int> DispatchPendingUserNotificationsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// P03 Task 3 (A-04/Y5): Vakti gelen hatırlatmaları kullanıcının <b>aktif cihazlarına gerçekten gönderir</b>.
/// Önceden yalnız <c>MarkSent</c> çağrılıyordu — bildirim "gönderildi" görünüyor ama kimseye ulaşmıyordu.
/// Sözleşme: en az bir cihaza teslim edilirse <c>Sent</c>; hiçbiri teslim edilemezse <c>MarkFailed</c>
/// (durum <c>Pending</c> kalır → bir sonraki turda tekrar denenir); FCM token'ı geçersizse token kapatılır.
/// </summary>
public sealed class NotificationDispatchProcessor : INotificationDispatchProcessor
{
    /// <summary>Tek turda gönderilecek in-app bildirim üst sınırı (tur süresini sınırlar).</summary>
    private const int PendingUserNotificationBatchSize = 100;

    private readonly ILessonReminderRepository _repository;
    private readonly IDeviceTokenRepository _deviceTokens;
    private readonly IUserNotificationRepository _userNotifications;
    private readonly IPushSender _pushSender;
    private readonly IUserNotificationPreferences _preferences;
    private readonly IStudentDirectory _students;
    private readonly IParentNotificationDirectory _parents;
    private readonly ILessonScheduleReader _schedules;
    private readonly EgitimUssu.Shared.Kernel.IClock _clock;

    public NotificationDispatchProcessor(
        ILessonReminderRepository repository,
        IDeviceTokenRepository deviceTokens,
        IUserNotificationRepository userNotifications,
        IPushSender pushSender,
        IUserNotificationPreferences preferences,
        EgitimUssu.Shared.Kernel.IClock clock,
        IStudentDirectory students,
        IParentNotificationDirectory parents,
        ILessonScheduleReader schedules)
    {
        _repository = repository;
        _deviceTokens = deviceTokens;
        _userNotifications = userNotifications;
        _pushSender = pushSender;
        _preferences = preferences;
        _clock = clock;
        _students = students;
        _parents = parents;
        _schedules = schedules;
    }

    public async Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        // C-04: Periyodik işin kendi span'ı. Olmazsa Npgsql bu turun her sorgusunu ayrı bir
        // **kök trace** olarak yayar (bkz. EgitimUssuActivitySource).
        using var activity = EgitimUssuActivitySource.Instance.StartActivity(
            "scan.notifications.reminders",
            ActivityKind.Internal);

        var now = _clock.UtcNow;
        var dueReminders = await _repository.ListDuePendingAsync(now, cancellationToken);
        var recipients = dueReminders.Select(r => r.RecipientUserId).Distinct().ToArray();
        var preferencesByUser = await _preferences.GetManyAsync(recipients, cancellationToken);
        var tokensByUser = (await _deviceTokens.ListActiveByUsersAsync(recipients, cancellationToken)).ToLookup(t => t.UserId);
        var sentCount = 0;

        foreach (var reminder in dueReminders)
        {
            if (reminder.RemindAtUtc.AddMinutes(30) < now || reminder.ScheduledLessonStartAtUtc <= now)
            {
                reminder.Expire(now);
                continue;
            }
            var current = await _schedules.ListReminderOccurrencesAsync(now, now.AddDays(7), reminder.LessonScheduleId, cancellationToken);
            if (!current.Any(o => o.OccurrenceStartUtc == reminder.OccurrenceStartUtc && o.StartAtUtc == reminder.ScheduledLessonStartAtUtc
                && o.ReminderOffsetMinutes > 0 && o.StartAtUtc.AddMinutes(-o.ReminderOffsetMinutes) == reminder.RemindAtUtc))
            {
                reminder.Cancel(now);
                continue;
            }
            if (reminder.RecipientUserId != reminder.TeacherUserId)
            {
                var owner = await _students.GetOwnerUserIdAsync(reminder.StudentId, cancellationToken);
                if (owner != reminder.RecipientUserId)
                {
                    var parents = await _parents.GetApprovedParentsForStudentAsync(reminder.StudentId, cancellationToken);
                    if (!parents.Any(p => p.ParentUserId == reminder.RecipientUserId && p.Prefs.LessonReminders))
                    {
                        reminder.Cancel(now);
                        continue;
                    }
                }
            }
            // M11-5 tercih kapısı: kullanıcı ders hatırlatmasını ya da tüm push'u kapattıysa gönderme.
            // Kuyruğu tıkamaması için gönderilmiş sayılır (tekrar denenmez).
            var preferences = preferencesByUser[reminder.RecipientUserId];
            if (!preferences.PushEnabled || !preferences.LessonReminderEnabled)
            {
                reminder.MarkSent(now);
                sentCount++;
                continue;
            }

            // Üçlü anahtardaki alıcının aktif cihazları; öğretmen/öğrenci/veli ayrı satırlardır.
            var tokens = tokensByUser[reminder.RecipientUserId].Where(t => t.IsActive).ToArray();
            if (tokens.Length == 0)
            {
                // Teslim edilecek cihaz yok; kuyruğu tıkamasın diye gönderilmiş sayılır.
                reminder.MarkSent(now);
                sentCount++;
                continue;
            }

            var anyDelivered = false;
            string? lastError = null;

            foreach (var token in tokens)
            {
                var result = await _pushSender.SendAsync(
                    token.Token,
                    new PushMessage(
                        reminder.Title,
                        reminder.Message,
                        new Dictionary<string, string>
                        {
                            ["route"] = "/scheduling",
                            ["lessonId"] = reminder.LessonScheduleId.ToString(),
                            ["occurrenceStartAtUtc"] = reminder.OccurrenceStartUtc.ToString("O")
                        }),
                    cancellationToken);

                switch (result.Status)
                {
                    case PushDeliveryStatus.Delivered:
                        anyDelivered = true;
                        break;
                    case PushDeliveryStatus.TokenInvalid:
                        token.Deactivate(now);
                        lastError = result.Error;
                        break;
                    case PushDeliveryStatus.TransientFailure:
                    default:
                        lastError = result.Error;
                        break;
                }
            }

            if (anyDelivered)
            {
                reminder.MarkSent(now);
                sentCount++;
            }
            else
            {
                reminder.MarkFailed(lastError ?? "Aktif cihaza teslim edilemedi.", now);
            }
        }

        if (dueReminders.Count > 0)
        {
            await _repository.SaveChangesAsync(cancellationToken);
            await _deviceTokens.SaveChangesAsync(cancellationToken);
        }

        return sentCount;
    }

    /// <summary>
    /// M11-3: In-app bildirim yazıldığı anda push gönderilmez (olay tüketicisi senkron ağ çağrısı yapmaz);
    /// bu tur onları toplu gönderir. <c>PushedOnUtc</c> yalnız gerçekten teslim edilince yazılır —
    /// başarısız olan üstel geri çekilmeyle yeniden denenir; 8 hatada Failed, TTL sonunda Expired olur.
    /// Teslim edilen ikinci kez gönderilmez; tercih/token okuması tur başına topludur.
    /// </summary>
    public async Task<int> DispatchPendingUserNotificationsAsync(CancellationToken cancellationToken = default)
    {
        // C-04: Periyodik işin kendi span'ı. Olmazsa Npgsql bu turun her sorgusunu ayrı bir
        // **kök trace** olarak yayar (bkz. EgitimUssuActivitySource).
        using var activity = EgitimUssuActivitySource.Instance.StartActivity(
            "scan.notifications.pending",
            ActivityKind.Internal);

        var now = _clock.UtcNow;
        var pending = await _userNotifications.ListPendingPushAsync(PendingUserNotificationBatchSize, cancellationToken, now);
        if (pending.Count == 0)
        {
            return 0;
        }

        var recipients = pending.Where(n => n.ExpiresOnUtc > now).Select(n => n.UserId).Distinct().ToArray();
        var preferencesByUser = await _preferences.GetManyAsync(recipients, cancellationToken);
        var tokensByUser = (await _deviceTokens.ListActiveByUsersAsync(recipients, cancellationToken)).ToLookup(t => t.UserId);
        var pushedCount = 0;

        foreach (var notification in pending)
        {
            // M11-5 tercih kapısı: in-app kaydı DURUR (bildirim merkezinde görünür) ama push atlanır.
            notification.ExpirePush(now);
            if (notification.PushStatus != NotificationPushStatus.Pending) continue;
            var preferences = preferencesByUser[notification.UserId];
            if (!preferences.PushEnabled || !IsKindEnabled(notification.Kind, preferences))
            {
                notification.MarkPushed(now);
                continue;
            }

            var tokens = tokensByUser[notification.UserId].Where(t => t.IsActive).ToArray();
            if (tokens.Length == 0)
            {
                // Cihazı yok: in-app kaydı durur, push kuyruğunu tıkamaması için gönderilmiş sayılır.
                notification.MarkPushed(now);
                continue;
            }

            var anyDelivered = false;

            foreach (var token in tokens)
            {
                var data = new Dictionary<string, string> { ["kind"] = notification.Kind.ToString() };
                if (!string.IsNullOrWhiteSpace(notification.Route))
                {
                    data["route"] = notification.Route;
                }

                var result = await _pushSender.SendAsync(
                    token.Token,
                    new PushMessage(notification.Title, notification.Body, data),
                    cancellationToken);

                switch (result.Status)
                {
                    case PushDeliveryStatus.Delivered:
                        anyDelivered = true;
                        break;
                    case PushDeliveryStatus.TokenInvalid:
                        token.Deactivate(now);
                        break;
                    case PushDeliveryStatus.TransientFailure:
                    default:
                        break;
                }
            }

            if (anyDelivered)
            {
                notification.MarkPushed(now);
                pushedCount++;
            }
            else notification.MarkPushFailed(now);
        }

        await _userNotifications.SaveChangesAsync(cancellationToken);
        await _deviceTokens.SaveChangesAsync(cancellationToken);

        return pushedCount;
    }

    /// <summary>
    /// M11-5: Bildirim türünü kullanıcının tercih bayrağına eşler. Karşılığı olmayan türler
    /// (mesaj, sistem/güvenlik) <b>koşulsuz</b> gönderilir — kullanıcı bunları kapatamaz.
    /// </summary>
    private static bool IsKindEnabled(UserNotificationKind kind, UserNotificationPreferences preferences)
        => kind switch
        {
            UserNotificationKind.LessonReminder or UserNotificationKind.LessonCompleted
                => preferences.LessonReminderEnabled,
            UserNotificationKind.AssignmentCreated or UserNotificationKind.AssignmentDue or UserNotificationKind.AssignmentMissed
                => preferences.HomeworkReminderEnabled,
            UserNotificationKind.PaymentDue or UserNotificationKind.PaymentOverdue
                => preferences.PaymentReminderEnabled,
            _ => true
        };
}

/// <summary>
/// A-07: Tur başına kira alır. Kirasız çalışırken ikinci bir uygulama örneği aynı <c>Pending</c>
/// satırları okuyup <b>aynı bildirimi iki kez</b> gönderebiliyordu.
/// </summary>
internal sealed class NotificationDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationDispatcher> logger) : LeasedBackgroundService(scopeFactory, logger)
{
    protected override string JobName => "notification-dispatch";

    protected override TimeSpan PollInterval => TimeSpan.FromSeconds(30);

    protected override ModuleDbContext ResolveDbContext(IServiceProvider services)
        => services.GetRequiredService<NotificationsDbContext>();

    protected override async Task ExecuteCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var processor = services.GetRequiredService<INotificationDispatchProcessor>();
        await processor.DispatchDueRemindersAsync(cancellationToken);
        await processor.DispatchPendingUserNotificationsAsync(cancellationToken);
    }
}
