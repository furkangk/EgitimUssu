using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.LessonSessions.Infrastructure;

/// <summary>
/// İki öğrenci profili birleştirildiğinde (Ö-C claim/merge), LessonSessions modülünün kaynak öğrenciye ait
/// ders seanslarını kanonik öğrenciye yeniden atar. Doğrudan cross-module DB erişimi yok — yalnızca integration event.
/// </summary>
internal sealed class LessonSessionsStudentMergedHandler : IIntegrationEventHandler
{
    private readonly LessonSessionsDbContext _db;

    public LessonSessionsStudentMergedHandler(LessonSessionsDbContext db) => _db = db;

    public bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Students"
            && integrationEvent.Name == "StudentProfilesMergedDomainEvent";

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent envelope)
        {
            return;
        }

        var payload = JsonSerializer.Deserialize<StudentProfilesMergedIntegrationEvent>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null)
        {
            return;
        }

        await _db.LessonSessions.Where(x => x.StudentId == payload.FromStudentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, payload.ToStudentId), cancellationToken);
    }
}
