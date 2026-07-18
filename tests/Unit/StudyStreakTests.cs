using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class StudyStreakTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Goal_StoresStreakThresholdPercent()
    {
        var goal = new StudyGoal(Guid.NewGuid(), Guid.NewGuid(), 120, null, null, null, null, 60, Now);
        Assert.Equal(60, goal.StreakThresholdPercent);

        goal.UpdateGoals(120, null, null, null, null, 75, Now);
        Assert.Equal(75, goal.StreakThresholdPercent);
    }

    [Fact]
    public async Task RecordCompleted_BelowThreshold_DoesNotRegisterStreakDay()
    {
        // 10 dk seans, hedef 120 dk / %60 → eşik 72 dk. Gün sayılmamalı.
        var studentId = Guid.NewGuid();
        var repo = new FakeStudyRepository(studentId, dailyGoal: 120, thresholdPercent: 60, existingTodayMinutes: 0);
        var svc = new StudyCompletionService(repo, new AchievementEvaluator(repo, new FakeIdGen(), new FakeClock(Now)), new FakeIdGen(), new FakeClock(Now));
        var session = StudySession.CreateManual(Guid.NewGuid(), studentId, "Mat", "Türev", 10, Now, null, false, false, Now);
        repo.Seed(session);

        await svc.RecordCompletedAsync(session, CancellationToken.None);

        Assert.Equal(0, repo.Streak?.CurrentStreakDays ?? 0);
    }

    [Fact]
    public async Task RecordCompleted_AtThreshold_RegistersStreakDay()
    {
        var studentId = Guid.NewGuid();
        var repo = new FakeStudyRepository(studentId, dailyGoal: 120, thresholdPercent: 60, existingTodayMinutes: 62);
        var svc = new StudyCompletionService(repo, new AchievementEvaluator(repo, new FakeIdGen(), new FakeClock(Now)), new FakeIdGen(), new FakeClock(Now));
        var session = StudySession.CreateManual(Guid.NewGuid(), studentId, "Mat", "Türev", 10, Now, null, false, false, Now); // 62+10=72=eşik
        repo.Seed(session);

        await svc.RecordCompletedAsync(session, CancellationToken.None);

        Assert.Equal(1, repo.Streak?.CurrentStreakDays);
    }
}

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}

internal sealed class FakeIdGen : IIdGenerator
{
    public Guid New() => Guid.NewGuid();
}

internal sealed class FakeStudyRepository : IStudyRepository
{
    private readonly Guid _studentId;
    private readonly int _dailyGoal;
    private readonly int _thresholdPercent;
    private readonly List<StudySession> _sessions = new();

    public FakeStudyRepository(Guid studentId, int dailyGoal, int thresholdPercent, int existingTodayMinutes)
    {
        _studentId = studentId;
        _dailyGoal = dailyGoal;
        _thresholdPercent = thresholdPercent;
        if (existingTodayMinutes > 0)
        {
            var reference = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
            _sessions.Add(StudySession.CreateManual(
                Guid.NewGuid(), studentId, "Mat", "Onceki", existingTodayMinutes, reference, null, false, false, reference));
        }
    }

    public StudyStreak? Streak { get; private set; }

    public void Seed(StudySession session) => _sessions.Add(session);

    public Task<IReadOnlyList<StudySession>> ListCompletedSessionsAsync(
        Guid studentId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StudySession>>(_sessions);

    public Task<StudyGoal?> GetActiveGoalAsync(Guid studentId, CancellationToken cancellationToken)
        => Task.FromResult<StudyGoal?>(new StudyGoal(
            Guid.NewGuid(), _studentId, _dailyGoal, null, null, null, null, _thresholdPercent, DateTime.UtcNow));

    public Task<StudyStreak?> GetStreakAsync(Guid studentId, CancellationToken cancellationToken)
        => Task.FromResult(Streak);

    public Task AddStreakAsync(StudyStreak streak, CancellationToken cancellationToken)
    {
        Streak = streak;
        return Task.CompletedTask;
    }

    public Task<StudyTopic?> GetTopicAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken)
        => Task.FromResult<StudyTopic?>(null);

    public Task AddTopicAsync(StudyTopic topic, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<int> SumEffectiveMinutesAsync(Guid studentId, CancellationToken cancellationToken) => Task.FromResult(0);

    public Task<int> CountCompletedSessionsAsync(Guid studentId, CancellationToken cancellationToken) => Task.FromResult(0);

    public Task<int> CountTestsAsync(Guid studentId, CancellationToken cancellationToken) => Task.FromResult(0);

    public Task<IReadOnlyList<Achievement>> ListCatalogAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Achievement>>(Array.Empty<Achievement>());

    public Task<IReadOnlyList<StudentAchievement>> ListEarnedAsync(Guid studentId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StudentAchievement>>(Array.Empty<StudentAchievement>());

    public Task AddEarnedAsync(StudentAchievement earned, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Kullanılmayan üyeler
    public Task<StudyStudent?> GetLinkAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task AddLinkAsync(StudyStudent link, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<StudySession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<StudySession?> GetActiveSessionAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IReadOnlyList<StudySession>> ListSessionsAsync(Guid studentId, DateTime? fromUtc, DateTime? toUtc, string? subject, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task AddSessionAsync(StudySession session, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<TestResult?> GetTestAsync(Guid testResultId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IReadOnlyList<TestResult>> ListTestsAsync(Guid studentId, string? subject, string? topic, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task AddTestAsync(TestResult testResult, CancellationToken cancellationToken) => throw new NotImplementedException();
    public void RemoveTest(TestResult testResult) => throw new NotImplementedException();
    public Task AddGoalAsync(StudyGoal goal, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IReadOnlyList<StudentSubjectCatalog>> ListCatalogSubjectsAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<StudentSubjectCatalog?> GetCatalogSubjectAsync(Guid subjectId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task AddCatalogSubjectAsync(StudentSubjectCatalog subject, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task RemoveCatalogSubjectAsync(StudentSubjectCatalog subject, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IReadOnlyList<StudentTopicCatalog>> ListCatalogTopicsAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IReadOnlyList<StudentTopicCatalog>> ListCatalogTopicsBySubjectAsync(Guid subjectId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<StudentTopicCatalog?> GetCatalogTopicAsync(Guid topicId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task AddCatalogTopicAsync(StudentTopicCatalog topic, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task RemoveCatalogTopicAsync(StudentTopicCatalog topic, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IReadOnlyList<StudyNote>> ListNotesAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<StudyNote?> GetNoteAsync(Guid noteId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task AddNoteAsync(StudyNote note, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task RemoveNoteAsync(StudyNote note, CancellationToken cancellationToken) => throw new NotImplementedException();
}
