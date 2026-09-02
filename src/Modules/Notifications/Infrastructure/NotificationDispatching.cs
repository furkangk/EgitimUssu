using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    private readonly EgitimUssu.Shared.Kernel.IClock _clock;

    public NotificationDispatchProcessor(
        ILessonReminderRepository repository,
        IDeviceTokenRepository deviceTokens,
        IUserNotificationRepository userNotifications,
        IPushSender pushSender,
        IUserNotificationPreferences preferences,
        EgitimUssu.Shared.Kernel.IClock clock)
    {
        _repository = repository;
        _deviceTokens = deviceTokens;
        _userNotifications = userNotifications;
        _pushSender = pushSender;
        _preferences = preferences;
        _clock = clock;
    }

    public async Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var dueReminders = await _repository.ListDuePendingAsync(now, cancellationToken);
        var sentCount = 0;

        foreach (var reminder in dueReminders)
        {
            // M11-5 tercih kapısı: kullanıcı ders hatırlatmasını ya da tüm push'u kapattıysa gönderme.
            // Kuyruğu tıkamaması için gönderilmiş sayılır (tekrar denenmez).
            var preferences = await _preferences.GetAsync(reminder.TeacherUserId, cancellationToken);
            if (!preferences.PushEnabled || !preferences.LessonReminderEnabled)
            {
                reminder.MarkSent(now);
                sentCount++;
                continue;
            }

            // Alıcı: hatırlatmanın sahibi olan öğretmen kullanıcısı. Kendi-ders (Ç-06) kayıtlarında
            // TeacherUserId boştur; öğrenciye teslim StudentId → UserId eşlemesi gerektirir (P07 read-model).
            var tokens = await _deviceTokens.ListActiveByUserAsync(reminder.TeacherUserId, cancellationToken);
            if (tokens.Count == 0)
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
                            ["lessonId"] = reminder.LessonScheduleId.ToString()
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
    /// başarısız olan bir sonraki turda tekrar denenir, teslim edilen ikinci kez gönderilmez.
    /// </summary>
    public async Task<int> DispatchPendingUserNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var pending = await _userNotifications.ListPendingPushAsync(PendingUserNotificationBatchSize, cancellationToken);
        if (pending.Count == 0)
        {
            return 0;
        }

        var pushedCount = 0;

        foreach (var notification in pending)
        {
            // M11-5 tercih kapısı: in-app kaydı DURUR (bildirim merkezinde görünür) ama push atlanır.
            var preferences = await _preferences.GetAsync(notification.UserId, cancellationToken);
            if (!preferences.PushEnabled || !IsKindEnabled(notification.Kind, preferences))
            {
                notification.MarkPushed(now);
                continue;
            }

            var tokens = await _deviceTokens.ListActiveByUserAsync(notification.UserId, cancellationToken);
            if (tokens.Count == 0)
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

internal sealed class NotificationDispatcher : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(IServiceScopeFactory scopeFactory, ILogger<NotificationDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<INotificationDispatchProcessor>();
                await processor.DispatchDueRemindersAsync(stoppingToken);
                await processor.DispatchPendingUserNotificationsAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Notification dispatch cycle failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
