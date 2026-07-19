using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

public interface IParentWeeklySummaryProcessor
{
    // O hafta için tüm Premium + WeeklyProgressSummary açık velilere bir WeeklySummary bildirimi üretir (haftada bir).
    Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}

public sealed class ParentWeeklySummaryProcessor : IParentWeeklySummaryProcessor
{
    private readonly IParentNotificationDirectory _directory;
    private readonly IParentNotificationRepository _repository;
    private readonly IIdGenerator _idGenerator;

    public ParentWeeklySummaryProcessor(
        IParentNotificationDirectory directory,
        IParentNotificationRepository repository,
        IIdGenerator idGenerator)
    {
        _directory = directory;
        _repository = repository;
        _idGenerator = idGenerator;
    }

    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var isoWeek = ISOWeek.GetWeekOfYear(nowUtc);
        var isoYear = ISOWeek.GetYear(nowUtc);
        var created = 0;
        var handledParents = new HashSet<Guid>();

        var targets = await _directory.ListAllApprovedTargetsAsync(cancellationToken);
        foreach (var entry in targets)
        {
            var target = entry.Target;

            // Premium + haftalık özet tercihi açık olmalı; her veli hafta başına bir kez.
            if (target.Tier != MembershipTier.Premium || !target.Prefs.WeeklyProgressSummary)
            {
                continue;
            }

            if (!handledParents.Add(target.ParentUserId))
            {
                continue;
            }

            var dedupKey = WeeklyDedupKey(target.ParentUserId, isoYear, isoWeek);
            if (await _repository.HasProcessedAsync(dedupKey, cancellationToken))
            {
                continue;
            }

            await _repository.AddAsync(
                new ParentNotification(
                    _idGenerator.New(),
                    target.ParentUserId,
                    entry.StudentId,
                    ParentNotificationType.WeeklySummary,
                    "Haftalık özet hazır",
                    "Çocuğunuzun haftalık gelişim özeti hazır.",
                    nowUtc),
                cancellationToken);

            _repository.MarkProcessed(dedupKey, $"weekly:{isoYear}-W{isoWeek}", nowUtc);
            created++;
        }

        if (created > 0)
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    // Hafta+veli için deterministik Guid (ProcessedIntegrationEvent dedup anahtarı). Aynı hafta tekrar üretilmez.
    internal static Guid WeeklyDedupKey(Guid parentUserId, int isoYear, int isoWeek)
    {
        var raw = $"weekly:{parentUserId:N}:{isoYear}:{isoWeek}";
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(raw));
        return new Guid(bytes);
    }
}

internal sealed class ParentWeeklySummaryService : BackgroundService
{
    // Haftalık iş; sık poll gerekmez. 6 saatte bir tetikler, dedup hafta anahtarı ile korunur.
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger<ParentWeeklySummaryService> _logger;

    public ParentWeeklySummaryService(IServiceScopeFactory scopeFactory, IClock clock, ILogger<ParentWeeklySummaryService> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IParentWeeklySummaryProcessor>();
                await processor.RunAsync(_clock.UtcNow, stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Parent weekly summary cycle failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
