using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Study.Infrastructure;

/// <summary>
/// İki öğrenci profili birleştirildiğinde (Ö-C claim/merge), Study modülünün kaynak öğrenciye ait tüm
/// çalışma verisini kanonik öğrenciye yeniden atar. <c>StudyStudent</c> birincil anahtarı (Id)
/// doğrudan öğrenci kimliği olduğundan yeniden atama PK çakışmasına yol açar; kanonik profil kendi
/// tercihlerini koruduğundan kaynak satır silinir. Doğrudan cross-module DB erişimi yok — yalnızca integration event.
/// Replay koruması ortak inbox üzerinden (<see cref="IdempotentIntegrationEventHandler"/>, EventId+Handler).
/// </summary>
internal sealed class StudyStudentMergedHandler : IdempotentIntegrationEventHandler
{
    public StudyStudentMergedHandler(StudyDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    private StudyDbContext StudyDb => (StudyDbContext)DbContext;

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

        var from = payload.FromStudentId;
        var to = payload.ToStudentId;

        await StudyDb.StudySessions.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await StudyDb.TestResults.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await StudyDb.MockExams.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await StudyDb.StudyGoals.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await StudyDb.StudyStreaks.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await StudyDb.StudentAchievements.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await StudyDb.StudyTopics.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await StudyDb.StudentSubjectCatalogs.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await StudyDb.StudentTopicCatalogs.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await StudyDb.StudyNotes.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);

        // StudyStudent birincil anahtarı öğrenci kimliğidir; kanonik profil kendi tercih satırını korur.
        await StudyDb.StudyStudents.Where(x => x.Id == from)
            .ExecuteDeleteAsync(cancellationToken);

        return true;
    }
}
