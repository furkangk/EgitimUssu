using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.LessonSessions.Infrastructure;

/// <summary>
/// İki öğrenci profili birleştirildiğinde (Ö-C claim/merge), LessonSessions modülünün kaynak öğrenciye ait
/// ders seanslarını kanonik öğrenciye yeniden atar. Doğrudan cross-module DB erişimi yok — yalnızca integration event.
/// Replay koruması ortak inbox üzerinden (<see cref="IdempotentIntegrationEventHandler"/>, EventId+Handler).
/// </summary>
internal sealed class LessonSessionsStudentMergedHandler : IdempotentIntegrationEventHandler
{
    public LessonSessionsStudentMergedHandler(LessonSessionsDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    private LessonSessionsDbContext LessonSessionsDb => (LessonSessionsDbContext)DbContext;

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

        await LessonSessionsDb.LessonSessions.Where(x => x.StudentId == payload.FromStudentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, payload.ToStudentId), cancellationToken);

        return true;
    }
}
