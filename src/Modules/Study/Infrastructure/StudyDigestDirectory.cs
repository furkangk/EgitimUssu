using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Study.Infrastructure;

/// <summary>
/// Veli paneli için canlı çalışma özeti (Veli V-F). Son 7 günün tamamlanmış seanslarından toplam/ders bazlı
/// dakika + güncel streak döner. `ChildProgressSnapshot`'taki atıl `WeeklyStudyMinutes`/`StudyStreakDays`
/// alanlarının yerini alır (Parents keşif bug'ı).
/// </summary>
internal sealed class StudyDigestDirectory : IStudyDigestDirectory
{
    private readonly StudyDbContext _dbContext;

    public StudyDigestDirectory(StudyDbContext dbContext) => _dbContext = dbContext;

    public async Task<StudyDigest> GetWeeklyDigestAsync(Guid studentId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var fromUtc = nowUtc.AddDays(-7);

        var sessions = await _dbContext.StudySessions
            .Where(s => s.StudentId == studentId
                && s.Status == StudySessionStatus.Completed
                && s.EndedAtUtc != null
                && s.EndedAtUtc >= fromUtc
                && s.EndedAtUtc <= nowUtc)
            .Select(s => new { s.Subject, s.EffectiveMinutes })
            .ToArrayAsync(cancellationToken);

        var totalMinutes = sessions.Sum(s => s.EffectiveMinutes);
        var breakdown = sessions
            .GroupBy(s => s.Subject)
            .Select(g => new StudySubjectMinutes(g.Key, g.Sum(s => s.EffectiveMinutes)))
            .OrderByDescending(x => x.Minutes)
            .ToArray();

        var streak = await _dbContext.StudyStreaks
            .Where(s => s.StudentId == studentId)
            .Select(s => (int?)s.CurrentStreakDays)
            .FirstOrDefaultAsync(cancellationToken);

        return new StudyDigest(totalMinutes, streak ?? 0, breakdown);
    }
}
