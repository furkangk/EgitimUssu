using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Observability;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;

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
    private readonly ILocalCalendar _calendar;

    public ParentWeeklySummaryProcessor(
        IParentNotificationDirectory directory,
        IParentNotificationRepository repository,
        IIdGenerator idGenerator,
        ILocalCalendar calendar)
    {
        _directory = directory;
        _repository = repository;
        _idGenerator = idGenerator;
        _calendar = calendar;
    }

    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // C-04: Periyodik işin kendi span'ı. Olmazsa Npgsql bu turun her sorgusunu ayrı bir
        // **kök trace** olarak yayar (bkz. EgitimUssuActivitySource).
        using var activity = EgitimUssuActivitySource.Instance.StartActivity(
            "scan.notifications.parent_weekly",
            ActivityKind.Internal);

        // P16 T6 / K-19: hafta Türkiye haftasıdır — Pazartesi 00:00 TR'de (Pazar 21:00Z) yeni hafta başlar. Eskiden UTC
        // anın ISO haftasıydı; Pazar 21:00–24:00Z turu bir önceki haftanın anahtarıyla özeti atlıyordu.
        var localMidnight = _calendar.ToLocalDate(nowUtc).ToDateTime(TimeOnly.MinValue);
        var isoWeek = ISOWeek.GetWeekOfYear(localMidnight);
        var isoYear = ISOWeek.GetYear(localMidnight);
        var created = 0;
        Guid? cursor = null;
        do
        {
            var page = await _directory.ListApprovedTargetPageAsync(cursor, 200, cancellationToken);
            var eligible = page.Items.Where(x => x.Target.Tier == MembershipTier.Premium && x.Target.Prefs.WeeklyProgressSummary)
                .DistinctBy(x => (x.Target.ParentUserId, x.StudentId)).ToArray();
            var keys = eligible.Select(x => WeeklyDedupKey(x.Target.ParentUserId, x.StudentId, isoYear, isoWeek)).ToArray();
            var processed = await _repository.GetProcessedAsync(keys, cancellationToken);
            var pageCreated = 0;
            foreach (var entry in eligible)
            {
                var key = WeeklyDedupKey(entry.Target.ParentUserId, entry.StudentId, isoYear, isoWeek);
                if (processed.Contains(key)) continue;
                await _repository.AddAsync(new ParentNotification(_idGenerator.New(), entry.Target.ParentUserId,
                    entry.StudentId, ParentNotificationType.WeeklySummary, "Haftalık özet hazır",
                    "Çocuğunuzun haftalık gelişim özeti hazır.", nowUtc), cancellationToken);
                _repository.MarkProcessed(key, $"weekly-v2:{isoYear}-W{isoWeek}", nowUtc);
                pageCreated++;
            }
            if (pageCreated > 0) await _repository.SaveChangesAsync(cancellationToken);
            created += pageCreated;
            cursor = page.NextLinkId;
        } while (cursor.HasValue);

        return created;
    }

    // Hafta+veli+öğrenci için deterministik Guid (ProcessedIntegrationEvent dedup anahtarı). Aynı hafta tekrar üretilmez.
    internal static Guid WeeklyDedupKey(Guid parentUserId, Guid studentId, int isoYear, int isoWeek)
    {
        var raw = $"weekly-v2:{parentUserId:N}:{studentId:N}:{isoYear}:{isoWeek}";
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(raw));
        return new Guid(bytes);
    }
}

internal sealed class ParentWeeklySummaryService(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<ParentWeeklySummaryService> logger) : LeasedBackgroundService(scopeFactory, logger)
{
    protected override string JobName => "parent-weekly-summary";

    // Haftalık iş; sık poll gerekmez. 6 saatte bir tetikler, dedup hafta anahtarı ile korunur.
    // A-07: Hafta anahtarı zaten çift göndermeyi engelliyordu; kira buna ek olarak iki örneğin
    // aynı anda tüm velileri taramasını (gereksiz yük) da önler.
    protected override TimeSpan PollInterval => TimeSpan.FromHours(6);

    protected override ModuleDbContext ResolveDbContext(IServiceProvider services)
        => services.GetRequiredService<NotificationsDbContext>();

    protected override Task ExecuteCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var processor = services.GetRequiredService<IParentWeeklySummaryProcessor>();
        return processor.RunAsync(clock.UtcNow, cancellationToken);
    }
}
