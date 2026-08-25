using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

/// <summary>
/// İki öğrenci profili birleştirildiğinde (Ö-C claim/merge), Scheduling modülünün kaynak öğrenciye ait
/// kayıtlarını kanonik öğrenciye yeniden atar. Doğrudan cross-module DB erişimi yok — yalnızca integration event.
/// Replay koruması ortak inbox üzerinden (<see cref="IdempotentIntegrationEventHandler"/>, EventId+Handler).
/// </summary>
internal sealed class SchedulingStudentMergedHandler : IdempotentIntegrationEventHandler
{
    public SchedulingStudentMergedHandler(SchedulingDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    private SchedulingDbContext SchedulingDb => (SchedulingDbContext)DbContext;

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Students"
            && integrationEvent.Name == "StudentProfilesMergedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<StudentProfilesMergedIntegrationEvent>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null)
        {
            return false;
        }

        // Ç-06: öğretmen dersleri + öğrencinin kendi dersleri (self) tek tabloda (lesson_schedules) tutulur.
        await SchedulingDb.LessonSchedules.Where(x => x.StudentId == payload.FromStudentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, payload.ToStudentId), cancellationToken);

        return true;
    }
}
