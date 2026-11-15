using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Study.Infrastructure;

/// <summary>Yalnız güncel kaynak sütunlarını okur; başka modülün nesnesini/DbContext'ini sızdırmaz.</summary>
public sealed class StudyStatisticsReader(StudyDbContext db) : IStudyStatisticsReader
{
    public async Task<StudyStatistics> ReadAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var sessions = await db.StudySessions.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.Status == StudySessionStatus.Completed && x.EndedAtUtc != null)
            .Select(x => new StudySessionStatistic(x.Id, x.Subject, x.Topic, x.EffectiveMinutes, x.EndedAtUtc!.Value))
            .ToArrayAsync(cancellationToken);
        var tests = await db.TestResults.AsNoTracking().Where(x => x.StudentId == studentId)
            .OrderBy(x => x.TakenOnUtc).ThenBy(x => x.Id)
            .Select(x => new StudyTestStatistic(x.Id, x.Subject, x.Topic, x.TotalQuestions, x.Net, x.TakenOnUtc))
            .ToArrayAsync(cancellationToken);
        return new(sessions, tests);
    }
}
