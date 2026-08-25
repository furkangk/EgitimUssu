using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

/// <summary>
/// İki öğrenci profili birleştirildiğinde (Ö-C claim/merge), Assignments modülünün kaynak öğrenciye ait
/// ödev ve ders notlarını kanonik öğrenciye yeniden atar. Doğrudan cross-module DB erişimi yok — yalnızca integration event.
/// Replay koruması ortak inbox üzerinden (<see cref="IdempotentIntegrationEventHandler"/>, EventId+Handler).
/// </summary>
internal sealed class AssignmentsStudentMergedHandler : IdempotentIntegrationEventHandler
{
    public AssignmentsStudentMergedHandler(AssignmentsDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    private AssignmentsDbContext AssignmentsDb => (AssignmentsDbContext)DbContext;

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

        await AssignmentsDb.Assignments.Where(x => x.StudentId == payload.FromStudentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, payload.ToStudentId), cancellationToken);
        await AssignmentsDb.LessonNotes.Where(x => x.StudentId == payload.FromStudentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, payload.ToStudentId), cancellationToken);

        return true;
    }
}
