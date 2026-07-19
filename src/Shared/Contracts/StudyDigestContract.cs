namespace EgitimUssu.Shared.Contracts;

public sealed record StudySubjectMinutes(string Subject, int Minutes);

public sealed record StudyDigest(int WeeklyStudyMinutes, int StreakDays, IReadOnlyCollection<StudySubjectMinutes> SubjectBreakdown);

public interface IStudyDigestDirectory
{
    // studentId için son 7 günün toplam çalışma dk + güncel streak + ders bazlı dağılım.
    Task<StudyDigest> GetWeeklyDigestAsync(Guid studentId, DateTime nowUtc, CancellationToken cancellationToken);
}
