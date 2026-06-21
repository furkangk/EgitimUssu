using EgitimUssu.Modules.Notifications.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

public interface INotificationDispatchProcessor
{
    Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default);
}

internal sealed class NotificationDispatchProcessor : INotificationDispatchProcessor
{
    private readonly ILessonReminderRepository _repository;
    private readonly EgitimUssu.Shared.Kernel.IClock _clock;

    public NotificationDispatchProcessor(ILessonReminderRepository repository, EgitimUssu.Shared.Kernel.IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var dueReminders = await _repository.ListDuePendingAsync(_clock.UtcNow, cancellationToken);
        foreach (var reminder in dueReminders)
        {
            reminder.MarkSent(_clock.UtcNow);
        }

        if (dueReminders.Count > 0)
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }

        return dueReminders.Count;
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
