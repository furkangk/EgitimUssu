using System.Text.Json;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>Bildirim geçmişi ve teslim durumu korunur; birleşen haftalık özetler veli/yerel hafta başına tektir.</summary>
public sealed class NotificationsStudentMergedHandler(NotificationsDbContext db, IClock clock, ILocalCalendar calendar)
    : IdempotentIntegrationEventHandler(db, clock)
{
    public override bool CanHandle(IIntegrationEvent message)
        => message.SourceModule == "Students" && message.Name == "StudentProfilesMergedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<StudentProfilesMergedIntegrationEvent>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null || payload.FromStudentId == Guid.Empty || payload.ToStudentId == Guid.Empty || payload.FromStudentId == payload.ToStudentId)
            throw new PoisonMessageException("Birleşme öğrenci kimlikleri geçersiz.");
        var from = payload.FromStudentId; var to = payload.ToStudentId;
        await ParentNotificationRepository.AcquireWeeklySummaryLockAsync(db, cancellationToken);
        var summaries = await db.ParentNotifications.AsNoTracking()
            .Where(x => (x.StudentId == from || x.StudentId == to) && x.Type == ParentNotificationType.WeeklySummary)
            .ToArrayAsync(cancellationToken);
        var groups = summaries.GroupBy(x => (x.ParentUserId, Week: ParentWeeklySummaryProcessor.WeekOf(x.CreatedOnUtc, calendar))).ToArray();
        var redundant = groups.SelectMany(g => g.OrderBy(x => x.StudentId == to ? 0 : 1)
            .ThenBy(x => x.CreatedOnUtc).ThenBy(x => x.Id).Skip(1)).Select(x => x.Id).ToArray();
        await db.ParentNotifications.Where(x => redundant.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        await db.ParentNotifications.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await db.LessonReminders.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);

        var markers = groups.Select(g => new ProcessedIntegrationEvent(
            ParentWeeklySummaryProcessor.WeeklyDedupKey(g.Key.ParentUserId, to, g.Key.Week.Year, g.Key.Week.Week),
            $"weekly-v2:{g.Key.Week.Year}-W{g.Key.Week.Week}", Clock.UtcNow)).ToArray();
        var keys = markers.Select(x => x.Id).ToArray();
        var existing = await db.ProcessedIntegrationEvents.Where(x => keys.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(cancellationToken);
        db.ProcessedIntegrationEvents.AddRange(markers.Where(x => !existing.Contains(x.Id)));
        return true;
    }
}
