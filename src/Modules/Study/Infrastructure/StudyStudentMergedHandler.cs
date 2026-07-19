using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Study.Infrastructure;

/// <summary>
/// İki öğrenci profili birleştirildiğinde (Ö-C claim/merge), Study modülünün kaynak öğrenciye ait tüm
/// çalışma verisini kanonik öğrenciye yeniden atar. <c>StudyStudent</c> birincil anahtarı (Id)
/// doğrudan öğrenci kimliği olduğundan yeniden atama PK çakışmasına yol açar; kanonik profil kendi
/// tercihlerini koruduğundan kaynak satır silinir. Doğrudan cross-module DB erişimi yok — yalnızca integration event.
/// </summary>
internal sealed class StudyStudentMergedHandler : IIntegrationEventHandler
{
    private readonly StudyDbContext _db;

    public StudyStudentMergedHandler(StudyDbContext db) => _db = db;

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

        var from = payload.FromStudentId;
        var to = payload.ToStudentId;

        await _db.StudySessions.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await _db.TestResults.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await _db.MockExams.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await _db.StudyGoals.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await _db.StudyStreaks.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await _db.StudentAchievements.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await _db.StudyTopics.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await _db.StudentSubjectCatalogs.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await _db.StudentTopicCatalogs.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await _db.StudyNotes.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);

        // StudyStudent birincil anahtarı öğrenci kimliğidir; kanonik profil kendi tercih satırını korur.
        await _db.StudyStudents.Where(x => x.Id == from)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
