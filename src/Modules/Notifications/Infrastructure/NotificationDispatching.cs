using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Infrastructure.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

public interface INotificationDispatchProcessor
{
    Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// P03 Task 3 (A-04/Y5): Vakti gelen hatırlatmaları kullanıcının <b>aktif cihazlarına gerçekten gönderir</b>.
/// Önceden yalnız <c>MarkSent</c> çağrılıyordu — bildirim "gönderildi" görünüyor ama kimseye ulaşmıyordu.
/// Sözleşme: en az bir cihaza teslim edilirse <c>Sent</c>; hiçbiri teslim edilemezse <c>MarkFailed</c>
/// (durum <c>Pending</c> kalır → bir sonraki turda tekrar denenir); FCM token'ı geçersizse token kapatılır.
/// </summary>
public sealed class NotificationDispatchProcessor : INotificationDispatchProcessor
{
    private readonly ILessonReminderRepository _repository;
    private readonly IDeviceTokenRepository _deviceTokens;
    private readonly IPushSender _pushSender;
    private readonly EgitimUssu.Shared.Kernel.IClock _clock;

    public NotificationDispatchProcessor(
        ILessonReminderRepository repository,
        IDeviceTokenRepository deviceTokens,
        IPushSender pushSender,
        EgitimUssu.Shared.Kernel.IClock clock)
    {
        _repository = repository;
        _deviceTokens = deviceTokens;
        _pushSender = pushSender;
        _clock = clock;
    }

    public async Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var dueReminders = await _repository.ListDuePendingAsync(now, cancellationToken);
        var sentCount = 0;

        foreach (var reminder in dueReminders)
        {
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
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Notification dispatch cycle failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
