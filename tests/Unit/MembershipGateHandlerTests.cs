using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class MembershipGateHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task ListSessions_Free_ClampsFromToLast30Days()
    {
        var repo = new CapturingStudyRepository();
        var handler = new ListStudySessionsQueryHandler(repo, Resolver(MembershipTier.Free), new FakeClock());

        await handler.Handle(new ListStudySessionsQuery(StudentId, FromUtc: null, ToUtc: null, Subject: null), CancellationToken.None);

        Assert.Equal(Now.AddDays(-30), repo.CapturedSessionsFrom);
    }

    [Fact]
    public async Task ListSessions_Free_KeepsTighterRequestedFrom()
    {
        var repo = new CapturingStudyRepository();
        var handler = new ListStudySessionsQueryHandler(repo, Resolver(MembershipTier.Free), new FakeClock());
        var requested = Now.AddDays(-10);

        await handler.Handle(new ListStudySessionsQuery(StudentId, requested, ToUtc: null, Subject: null), CancellationToken.None);

        Assert.Equal(requested, repo.CapturedSessionsFrom);
    }

    [Fact]
    public async Task ListSessions_Premium_KeepsUnlimited()
    {
        var repo = new CapturingStudyRepository();
        var handler = new ListStudySessionsQueryHandler(repo, Resolver(MembershipTier.Premium), new FakeClock());

        await handler.Handle(new ListStudySessionsQuery(StudentId, FromUtc: null, ToUtc: null, Subject: null), CancellationToken.None);

        Assert.Null(repo.CapturedSessionsFrom);
    }

    [Fact]
    public async Task UpdateGoals_Free_WithTargetNet_ReturnsPremiumRequired()
    {
        var handler = new UpdateStudyGoalsCommandHandler(
            new CapturingStudyRepository(), NoopLinkResolver(), Resolver(MembershipTier.Free), new FakeIds(), new FakeClock());

        var result = await handler.Handle(
            new UpdateStudyGoalsCommand(StudentId, DailyGoalMinutes: 60, WeeklyGoalMinutes: null,
                TargetNet: 90m, TargetScore: null, Subject: null, StreakThresholdPercent: 60),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("study.premium_required", result.Error.Code);
    }

    // ---- Fakes ----

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class FakeIds : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string? UserId { get; init; }
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
        public bool IsAuthenticated => true;
    }

    private sealed class FakeMembershipDirectory : IMembershipDirectory
    {
        private readonly MembershipTier _tier;
        public FakeMembershipDirectory(MembershipTier tier) => _tier = tier;
        public Task<MembershipTier> GetTierAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(_tier);
    }

    private static StudyMembershipResolver Resolver(MembershipTier tier) =>
        new(new FakeCurrentUser { UserId = UserId.ToString() }, new FakeMembershipDirectory(tier));

    private static StudyLinkResolver NoopLinkResolver() =>
        new(new FakeCurrentUser { UserId = UserId.ToString() }, new CapturingStudyRepository(), new FakeClock());

    /// <summary>Yalnızca geçmiş-listeleme metotlarının argümanlarını yakalayan fake repository.</summary>
    private sealed class CapturingStudyRepository : IStudyRepository
    {
        public DateTime? CapturedSessionsFrom { get; private set; }
        public DateTime? CapturedTestsFrom { get; private set; }

        public Task<IReadOnlyList<StudySession>> ListSessionsAsync(
            Guid studentId, DateTime? fromUtc, DateTime? toUtc, string? subject, CancellationToken cancellationToken)
        {
            CapturedSessionsFrom = fromUtc;
            return Task.FromResult<IReadOnlyList<StudySession>>(Array.Empty<StudySession>());
        }

        public Task<IReadOnlyList<TestResult>> ListTestsAsync(
            Guid studentId, string? subject, string? topic, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
        {
            CapturedTestsFrom = fromUtc;
            return Task.FromResult<IReadOnlyList<TestResult>>(Array.Empty<TestResult>());
        }

        public Task<StudyStudent?> GetLinkAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddLinkAsync(StudyStudent link, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StudySession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StudySession?> GetActiveSessionAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<StudySession>> ListCompletedSessionsAsync(Guid studentId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> CountCompletedSessionsAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> SumEffectiveMinutesAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddSessionAsync(StudySession session, CancellationToken cancellationToken) => throw new NotImplementedException();
        public void RemoveSession(StudySession session) => throw new NotImplementedException();
        public Task<IReadOnlyList<StudySession>> ListCompletedSessionsByTopicAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<TestResult?> GetTestAsync(Guid testResultId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> CountTestsAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddTestAsync(TestResult testResult, CancellationToken cancellationToken) => throw new NotImplementedException();
        public void RemoveTest(TestResult testResult) => throw new NotImplementedException();
        public Task AddMockExamAsync(MockExam mockExam, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StudyGoal?> GetActiveGoalAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddGoalAsync(StudyGoal goal, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StudyStreak?> GetStreakAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddStreakAsync(StudyStreak streak, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StudyTopic?> GetTopicAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddTopicAsync(StudyTopic topic, CancellationToken cancellationToken) => throw new NotImplementedException();
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
        public Task<IReadOnlyList<Achievement>> ListCatalogAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<StudentAchievement>> ListEarnedAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddEarnedAsync(StudentAchievement earned, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
