using EgitimUssu.Modules.Study.Domain;

namespace EgitimUssu.Modules.Study.Application;

/// <summary>
/// Bir öğrenciye ait (sahiplik kapsamlı) komut/sorgu için ortak işaret arayüzü.
/// Tek bir açık-generik yetkilendirici (StudyOwnershipAuthorizer) bu arayüzü uygulayan tüm istekleri korur.
/// </summary>
public interface IStudentScopedRequest
{
    Guid StudentId { get; }
}

public sealed record StudySessionResponse(
    Guid Id,
    Guid StudentId,
    string Subject,
    string? Topic,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    int EffectiveMinutes,
    int BreakMinutes,
    string Status,
    string Source,
    string? PersonalNote,
    bool IsSharedWithParent,
    bool IsSharedWithTeacher,
    DateTime CreatedOnUtc);

/// <summary>Öğrencinin o an aktif (çalışan/molada) seansı ve takılı (unutulmuş) olup olmadığı.</summary>
public sealed record ActiveSessionResponse(StudySessionResponse Session, bool IsStale);

public sealed record SubjectMinutesResponse(string Subject, int EffectiveMinutes, int SessionCount);

public sealed record DayMinutesResponse(DateOnly Date, int EffectiveMinutes, int SessionCount);

public sealed record WeeklySummaryResponse(
    DateOnly WeekStartDate,
    int TotalEffectiveMinutes,
    int TotalBreakMinutes,
    int SessionCount,
    IReadOnlyCollection<SubjectMinutesResponse> PerSubject,
    IReadOnlyCollection<DayMinutesResponse> PerDay);

public sealed record TestResultResponse(
    Guid Id,
    Guid StudentId,
    string Subject,
    string? Topic,
    string? TestName,
    string TestType,
    int TotalQuestions,
    int Correct,
    int Wrong,
    int Blank,
    decimal Net,
    int PenaltyDivisor,
    int? DurationMinutes,
    DateTime TakenOnUtc,
    bool IsSharedWithParent,
    bool IsSharedWithTeacher,
    DateTime CreatedOnUtc);

public sealed record NetTrendPointResponse(DateTime TakenOnUtc, decimal Net, string? TestName, int TotalQuestions);

public sealed record NetTrendResponse(
    string? Subject,
    string? Topic,
    IReadOnlyCollection<NetTrendPointResponse> Points);

public sealed record StudyGoalResponse(
    Guid Id,
    Guid StudentId,
    int DailyGoalMinutes,
    int? WeeklyGoalMinutes,
    decimal? TargetNet,
    decimal? TargetScore,
    string? Subject,
    int StreakThresholdPercent,
    bool IsActive,
    DateTime UpdatedOnUtc);

public sealed record StreakResponse(
    int CurrentStreakDays,
    int LongestStreakDays,
    DateOnly? LastStudiedOnDate,
    int TotalStudyDays,
    bool StudiedToday,
    int TodayEffectiveMinutes);

public sealed record AchievementResponse(
    string Code,
    string Title,
    string Description,
    string Category,
    int Threshold,
    string? IconKey,
    bool Earned,
    DateTime? EarnedOnUtc,
    int CurrentValue);

public sealed record StudySharingResponse(
    bool ShareStudyWithParent,
    bool ShareTestsWithParent,
    bool ShareStudyWithTeacher,
    bool ShareTestsWithTeacher);

public sealed record StudyDashboardResponse(
    Guid StudentId,
    int TodayEffectiveMinutes,
    int TodayGoalMinutes,
    bool TodayGoalMet,
    int WeekEffectiveMinutes,
    int CurrentStreakDays,
    int LongestStreakDays,
    StudyGoalResponse? ActiveGoal,
    TestResultResponse? LastTest,
    IReadOnlyCollection<StudySessionResponse> RecentSessions,
    IReadOnlyCollection<AchievementResponse> RecentAchievements);

/// <summary>
/// Study modülünün tüm kalıcılık işlemlerini tek sınır (unit-of-work) içinde toplayan repository.
/// Tek SaveChanges; seans + streak + konu + rozet + bağ kayıtları atomik yazılır, domain olayları
/// aynı transaction'da Outbox'a düşer.
/// </summary>
public interface IStudyRepository
{
    // Öğrenci ↔ kullanıcı bağı + paylaşım tercihleri
    Task<StudyStudent?> GetLinkAsync(Guid studentId, CancellationToken cancellationToken);

    Task AddLinkAsync(StudyStudent link, CancellationToken cancellationToken);

    // Seanslar
    Task<StudySession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<StudySession?> GetActiveSessionAsync(Guid studentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudySession>> ListSessionsAsync(
        Guid studentId, DateTime? fromUtc, DateTime? toUtc, string? subject, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudySession>> ListCompletedSessionsAsync(
        Guid studentId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

    Task<int> CountCompletedSessionsAsync(Guid studentId, CancellationToken cancellationToken);

    Task<int> SumEffectiveMinutesAsync(Guid studentId, CancellationToken cancellationToken);

    Task AddSessionAsync(StudySession session, CancellationToken cancellationToken);

    void RemoveSession(StudySession session);

    Task<IReadOnlyList<StudySession>> ListCompletedSessionsByTopicAsync(
        Guid studentId, string subject, string topic, CancellationToken cancellationToken);

    // Testler
    Task<TestResult?> GetTestAsync(Guid testResultId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TestResult>> ListTestsAsync(
        Guid studentId, string? subject, string? topic, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);

    Task<int> CountTestsAsync(Guid studentId, CancellationToken cancellationToken);

    Task AddTestAsync(TestResult testResult, CancellationToken cancellationToken);

    void RemoveTest(TestResult testResult);

    // Çok dersli deneme
    Task AddMockExamAsync(MockExam mockExam, CancellationToken cancellationToken);

    // Hedefler
    Task<StudyGoal?> GetActiveGoalAsync(Guid studentId, CancellationToken cancellationToken);

    Task AddGoalAsync(StudyGoal goal, CancellationToken cancellationToken);

    // Streak
    Task<StudyStreak?> GetStreakAsync(Guid studentId, CancellationToken cancellationToken);

    Task AddStreakAsync(StudyStreak streak, CancellationToken cancellationToken);

    // Konu rollup
    Task<StudyTopic?> GetTopicAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken);

    Task AddTopicAsync(StudyTopic topic, CancellationToken cancellationToken);

    void RemoveTopic(StudyTopic topic);

    // Ders/konu kataloğu (öğrencinin tanımladığı)
    Task<IReadOnlyList<StudentSubjectCatalog>> ListCatalogSubjectsAsync(Guid studentId, CancellationToken cancellationToken);

    Task<StudentSubjectCatalog?> GetCatalogSubjectAsync(Guid subjectId, CancellationToken cancellationToken);

    Task AddCatalogSubjectAsync(StudentSubjectCatalog subject, CancellationToken cancellationToken);

    Task RemoveCatalogSubjectAsync(StudentSubjectCatalog subject, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentTopicCatalog>> ListCatalogTopicsAsync(Guid studentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentTopicCatalog>> ListCatalogTopicsBySubjectAsync(Guid subjectId, CancellationToken cancellationToken);

    Task<StudentTopicCatalog?> GetCatalogTopicAsync(Guid topicId, CancellationToken cancellationToken);

    Task AddCatalogTopicAsync(StudentTopicCatalog topic, CancellationToken cancellationToken);

    Task RemoveCatalogTopicAsync(StudentTopicCatalog topic, CancellationToken cancellationToken);

    // Öğrenci ders notları
    Task<IReadOnlyList<StudyNote>> ListNotesAsync(Guid studentId, CancellationToken cancellationToken);

    Task<StudyNote?> GetNoteAsync(Guid noteId, CancellationToken cancellationToken);

    Task AddNoteAsync(StudyNote note, CancellationToken cancellationToken);

    Task RemoveNoteAsync(StudyNote note, CancellationToken cancellationToken);

    // Başarımlar
    Task<IReadOnlyList<Achievement>> ListCatalogAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentAchievement>> ListEarnedAsync(Guid studentId, CancellationToken cancellationToken);

    Task AddEarnedAsync(StudentAchievement earned, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
