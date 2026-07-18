using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class StudySessionRecoveryHandlerTests
{
    private static readonly DateTime Start = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Start.AddHours(8);
    private static readonly Guid StudentId = Guid.NewGuid();

    [Fact]
    public async Task Recover_CompletesStuckSession_WithGivenMinutes()
    {
        var repo = new InMemoryStudyRepository();
        var session = StudySession.StartStopwatch(Guid.NewGuid(), StudentId, "Mat", null, false, false, Start);
        repo.Seed(session);

        var handler = new RecoverStudySessionCommandHandler(repo, Completion(repo), new FakeClock());
        var result = await handler.Handle(new RecoverStudySessionCommand(session.Id, EffectiveMinutes: 55), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StudySessionStatus.Completed, session.Status);
        Assert.Equal(55, session.EffectiveMinutes);
    }

    [Fact]
    public async Task Recover_MissingSession_ReturnsNotFound()
    {
        var repo = new InMemoryStudyRepository();
        var handler = new RecoverStudySessionCommandHandler(repo, Completion(repo), new FakeClock());

        var result = await handler.Handle(new RecoverStudySessionCommand(Guid.NewGuid(), 30), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("study.session_not_found", result.Error.Code);
    }

    [Fact]
    public async Task ActiveSession_ReturnsSessionWithStaleFlag()
    {
        var repo = new InMemoryStudyRepository();
        var session = StudySession.StartStopwatch(Guid.NewGuid(), StudentId, "Mat", null, false, false, Start);
        repo.Seed(session);

        var handler = new GetActiveSessionQueryHandler(repo, new FakeClock());
        var result = await handler.Handle(new GetActiveSessionQuery(StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(session.Id, result.Value!.Session.Id);
        Assert.True(result.Value.IsStale); // 8 saattir çalışıyor → takılı
    }

    [Fact]
    public async Task ActiveSession_NoActive_ReturnsNull()
    {
        var repo = new InMemoryStudyRepository();
        var handler = new GetActiveSessionQueryHandler(repo, new FakeClock());

        var result = await handler.Handle(new GetActiveSessionQuery(StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    private static StudyCompletionService Completion(IStudyRepository repo)
    {
        var ids = new FakeIds();
        var clock = new FakeClock();
        return new StudyCompletionService(repo, new AchievementEvaluator(repo, ids, clock), ids, clock);
    }

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class FakeIds : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    /// <summary>Recover/active-session handler'ları için gereken alt kümeyi tutan in-memory repository.</summary>
    private sealed class InMemoryStudyRepository : IStudyRepository
    {
        private readonly Dictionary<Guid, StudySession> _sessions = new();
        private StudyStreak? _streak;

        public void Seed(StudySession session) => _sessions[session.Id] = session;

        public Task<StudySession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
            => Task.FromResult(_sessions.TryGetValue(sessionId, out var s) ? s : null);

        public Task<StudySession?> GetActiveSessionAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(_sessions.Values.FirstOrDefault(s =>
                s.StudentId == studentId && s.Status is StudySessionStatus.Running or StudySessionStatus.Paused));

        public Task<IReadOnlyList<StudySession>> ListCompletedSessionsAsync(Guid studentId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StudySession>>(_sessions.Values
                .Where(s => s.StudentId == studentId && s.Status == StudySessionStatus.Completed
                    && (s.EndedAtUtc ?? s.StartedAtUtc) >= fromUtc && (s.EndedAtUtc ?? s.StartedAtUtc) < toUtc)
                .ToArray());

        public Task<int> CountCompletedSessionsAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(_sessions.Values.Count(s => s.StudentId == studentId && s.Status == StudySessionStatus.Completed));

        public Task<int> SumEffectiveMinutesAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(_sessions.Values.Where(s => s.StudentId == studentId).Sum(s => s.EffectiveMinutes));

        public Task<int> CountTestsAsync(Guid studentId, CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<StudyStreak?> GetStreakAsync(Guid studentId, CancellationToken cancellationToken) => Task.FromResult(_streak);

        public Task AddStreakAsync(StudyStreak streak, CancellationToken cancellationToken)
        {
            _streak = streak;
            return Task.CompletedTask;
        }

        public Task<StudyGoal?> GetActiveGoalAsync(Guid studentId, CancellationToken cancellationToken) => Task.FromResult<StudyGoal?>(null);

        public Task<StudyTopic?> GetTopicAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken) => Task.FromResult<StudyTopic?>(null);

        public Task AddTopicAsync(StudyTopic topic, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Achievement>> ListCatalogAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Achievement>>(Array.Empty<Achievement>());

        public Task<IReadOnlyList<StudentAchievement>> ListEarnedAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StudentAchievement>>(Array.Empty<StudentAchievement>());

        public Task AddEarnedAsync(StudentAchievement earned, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Kullanılmayanlar
        public Task<StudyStudent?> GetLinkAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddLinkAsync(StudyStudent link, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<StudySession>> ListSessionsAsync(Guid studentId, DateTime? fromUtc, DateTime? toUtc, string? subject, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddSessionAsync(StudySession session, CancellationToken cancellationToken) => throw new NotImplementedException();
        public void RemoveSession(StudySession session) => throw new NotImplementedException();
        public Task<IReadOnlyList<StudySession>> ListCompletedSessionsByTopicAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<TestResult?> GetTestAsync(Guid testResultId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<TestResult>> ListTestsAsync(Guid studentId, string? subject, string? topic, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddTestAsync(TestResult testResult, CancellationToken cancellationToken) => throw new NotImplementedException();
        public void RemoveTest(TestResult testResult) => throw new NotImplementedException();
        public Task AddMockExamAsync(MockExam mockExam, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddGoalAsync(StudyGoal goal, CancellationToken cancellationToken) => throw new NotImplementedException();
        public void RemoveTopic(StudyTopic topic) => throw new NotImplementedException();
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
}
