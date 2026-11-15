using System.Text.Json;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.LessonSessions.Infrastructure;

public sealed class LessonCancelledSessionHandler(LessonSessionsDbContext dbContext, IClock clock)
    : IdempotentIntegrationEventHandler(dbContext, clock)
{
    public override bool CanHandle(IIntegrationEvent integrationEvent) => integrationEvent.SourceModule == "Scheduling" &&
        integrationEvent.Name is "LessonCancellationAppliedDomainEvent" or "LessonScheduleDeletedDomainEvent";
    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<LessonCancellationApplied>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null) throw new PoisonMessageException("Olay yükü null olamaz.");
        if (envelope.Name == "LessonScheduleDeletedDomainEvent") payload = payload with { Scope = "All" };
        var sessions = await dbContext.LessonSessions.Where(s => s.LessonScheduleId == payload.LessonScheduleId &&
            (s.Status == LessonSessionStatus.Planned || s.Status == LessonSessionStatus.InProgress)).ToArrayAsync(cancellationToken);
        foreach (var session in sessions.Where(s => payload.Includes(s.OccurrenceStartUtc ?? s.PlannedStartAtUtc)))
            session.Cancel("Takvim dersi iptal edildi.", Clock.UtcNow);
        return true;
    }
}
