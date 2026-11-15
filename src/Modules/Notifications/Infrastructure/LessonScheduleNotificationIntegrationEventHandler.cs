using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>Olay yükündeki eski saati uygulamak yerine güncel kaynağı inbox transaction'ında uzlaştırır.</summary>
internal sealed class LessonScheduleNotificationIntegrationEventHandler(
    NotificationsDbContext dbContext, IClock clock, LessonReminderPlanningProcessor planner)
    : IdempotentIntegrationEventHandler(dbContext, clock)
{
    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Scheduling" && integrationEvent.Name is
            "LessonScheduledDomainEvent" or "LessonScheduleRescheduledDomainEvent" or "LessonOccurrenceRescheduledDomainEvent" or
            "LessonScheduleCancelledDomainEvent" or "LessonCancellationAppliedDomainEvent" or
            "LessonOccurrenceCancelledIntegrationEvent" or "LessonScheduleDeletedDomainEvent" or "LessonSeriesSplitDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(envelope.Payload);
        // Split hem eski hem yeni seriyi değiştirir; global pencere tek kilit altında ikisini uzlaştırır.
        if (envelope.Name == "LessonSeriesSplitDomainEvent") await planner.ReconcileAsync(null, cancellationToken);
        else
        {
            var id = json.RootElement.EnumerateObject().FirstOrDefault(p => p.Name.Equals("lessonScheduleId", StringComparison.OrdinalIgnoreCase)).Value;
            if (id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var scheduleId)) throw new PoisonMessageException("Ders kimliği geçersiz.");
            await planner.ReconcileAsync(scheduleId, cancellationToken);
        }
        return true;
    }
}
