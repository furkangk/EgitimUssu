using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class TestResultScoringIntegrityTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(3, null, 3, 6.67)]
    [InlineData(int.MaxValue, null, int.MaxValue, 10)]
    [InlineData(3, 4, 4, 7.5)]
    public async Task Editing_metadata_preserves_existing_scoring_unless_explicitly_changed(
        int originalDivisor, int? requestedDivisor, int expectedDivisor, decimal expectedNet)
    {
        var test = new TestResult(Guid.NewGuid(), Guid.NewGuid(), "Matematik", null, "Deneme", TestType.Subject,
            20, 10, 10, 0, originalDivisor, null, Now, false, false, Now);
        var handler = new EditTestResultCommandHandler(new TestRepository(test), new Clock());

        var result = await handler.Handle(new EditTestResultCommand(test.Id, "Matematik", "Yeni konu", "Subject",
            "Düzeltilmiş ad", 20, 10, 10, 0, requestedDivisor, null, Now), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedDivisor, test.PenaltyDivisor);
        Assert.Equal(expectedNet, test.Net);
        Assert.Equal("Düzeltilmiş ad", test.TestName);
    }

    private sealed class Clock : IClock { public DateTime UtcNow => Now; }

    private sealed class TestRepository(TestResult test) : IStudyRepository
    {
        public Task<StudyStudent?> GetLinkAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddLinkAsync(StudyStudent link, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StudySession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StudySession?> GetActiveSessionAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<StudySession>> ListSessionsAsync( Guid studentId, DateTime? fromUtc, DateTime? toUtc, string? subject, CancellationToken cancellationToken, EgitimUssu.Shared.Kernel.KeysetPage? page = null) => throw new NotImplementedException();
        public Task<IReadOnlyList<StudySession>> ListCompletedSessionsAsync( Guid studentId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> CountCompletedSessionsAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> SumEffectiveMinutesAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddSessionAsync(StudySession session, CancellationToken cancellationToken) => throw new NotImplementedException();
        public void RemoveSession(StudySession session) => throw new NotImplementedException();
        public Task<IReadOnlyList<StudySession>> ListCompletedSessionsByTopicAsync( Guid studentId, string subject, string topic, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<TestResult?> GetTestAsync(Guid testResultId, CancellationToken cancellationToken) => Task.FromResult<TestResult?>(test);
        public Task<IReadOnlyList<TestResult>> ListTestsAsync( Guid studentId, string? subject, string? topic, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken, EgitimUssu.Shared.Kernel.KeysetPage? page = null) => throw new NotImplementedException();
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
        public Task<IReadOnlyList<StudyNote>> ListNotesAsync(Guid studentId, CancellationToken cancellationToken, EgitimUssu.Shared.Kernel.KeysetPage? page = null) => throw new NotImplementedException();
        public Task<StudyNote?> GetNoteAsync(Guid noteId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddNoteAsync(StudyNote note, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task RemoveNoteAsync(StudyNote note, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<Achievement>> ListCatalogAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<StudentAchievement>> ListEarnedAsync(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddEarnedAsync(StudentAchievement earned, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
