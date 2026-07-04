using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Study.Infrastructure;

internal sealed class StudyRepository : IStudyRepository
{
    private readonly StudyDbContext _dbContext;

    public StudyRepository(StudyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<StudyStudent?> GetLinkAsync(Guid studentId, CancellationToken cancellationToken) =>
        _dbContext.StudyStudents.FirstOrDefaultAsync(x => x.Id == studentId, cancellationToken);

    public Task AddLinkAsync(StudyStudent link, CancellationToken cancellationToken) =>
        _dbContext.StudyStudents.AddAsync(link, cancellationToken).AsTask();

    public Task<StudySession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        _dbContext.StudySessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

    public Task<StudySession?> GetActiveSessionAsync(Guid studentId, CancellationToken cancellationToken) =>
        _dbContext.StudySessions.FirstOrDefaultAsync(
            x => x.StudentId == studentId
                 && (x.Status == StudySessionStatus.Running || x.Status == StudySessionStatus.Paused),
            cancellationToken);

    public async Task<IReadOnlyList<StudySession>> ListSessionsAsync(
        Guid studentId, DateTime? fromUtc, DateTime? toUtc, string? subject, CancellationToken cancellationToken)
    {
        var query = _dbContext.StudySessions
            .Where(x => x.StudentId == studentId && x.Status != StudySessionStatus.Discarded);

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.StartedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.StartedAtUtc < toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(subject))
        {
            query = query.Where(x => x.Subject == subject);
        }

        return await query.OrderByDescending(x => x.StartedAtUtc).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudySession>> ListCompletedSessionsAsync(
        Guid studentId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        return await _dbContext.StudySessions
            .Where(x => x.StudentId == studentId
                        && x.Status == StudySessionStatus.Completed
                        && x.EndedAtUtc != null
                        && x.EndedAtUtc >= fromUtc
                        && x.EndedAtUtc < toUtc)
            .OrderBy(x => x.EndedAtUtc)
            .ToArrayAsync(cancellationToken);
    }

    public Task<int> CountCompletedSessionsAsync(Guid studentId, CancellationToken cancellationToken) =>
        _dbContext.StudySessions.CountAsync(
            x => x.StudentId == studentId && x.Status == StudySessionStatus.Completed, cancellationToken);

    public async Task<int> SumEffectiveMinutesAsync(Guid studentId, CancellationToken cancellationToken)
    {
        return await _dbContext.StudySessions
            .Where(x => x.StudentId == studentId && x.Status == StudySessionStatus.Completed)
            .SumAsync(x => x.EffectiveMinutes, cancellationToken);
    }

    public Task AddSessionAsync(StudySession session, CancellationToken cancellationToken) =>
        _dbContext.StudySessions.AddAsync(session, cancellationToken).AsTask();

    public Task<TestResult?> GetTestAsync(Guid testResultId, CancellationToken cancellationToken) =>
        _dbContext.TestResults.FirstOrDefaultAsync(x => x.Id == testResultId, cancellationToken);

    public async Task<IReadOnlyList<TestResult>> ListTestsAsync(
        Guid studentId, string? subject, string? topic, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var query = _dbContext.TestResults.Where(x => x.StudentId == studentId);

        if (!string.IsNullOrWhiteSpace(subject))
        {
            query = query.Where(x => x.Subject == subject);
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            query = query.Where(x => x.Topic == topic);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.TakenOnUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.TakenOnUtc < toUtc.Value);
        }

        return await query.OrderByDescending(x => x.TakenOnUtc).ToArrayAsync(cancellationToken);
    }

    public Task<int> CountTestsAsync(Guid studentId, CancellationToken cancellationToken) =>
        _dbContext.TestResults.CountAsync(x => x.StudentId == studentId, cancellationToken);

    public Task AddTestAsync(TestResult testResult, CancellationToken cancellationToken) =>
        _dbContext.TestResults.AddAsync(testResult, cancellationToken).AsTask();

    public Task<StudyGoal?> GetActiveGoalAsync(Guid studentId, CancellationToken cancellationToken) =>
        _dbContext.StudyGoals
            .Where(x => x.StudentId == studentId && x.IsActive)
            .OrderByDescending(x => x.UpdatedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddGoalAsync(StudyGoal goal, CancellationToken cancellationToken) =>
        _dbContext.StudyGoals.AddAsync(goal, cancellationToken).AsTask();

    public Task<StudyStreak?> GetStreakAsync(Guid studentId, CancellationToken cancellationToken) =>
        _dbContext.StudyStreaks.FirstOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);

    public Task AddStreakAsync(StudyStreak streak, CancellationToken cancellationToken) =>
        _dbContext.StudyStreaks.AddAsync(streak, cancellationToken).AsTask();

    public Task<StudyTopic?> GetTopicAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken) =>
        _dbContext.StudyTopics.FirstOrDefaultAsync(
            x => x.StudentId == studentId && x.Subject == subject && x.Topic == topic, cancellationToken);

    public Task AddTopicAsync(StudyTopic topic, CancellationToken cancellationToken) =>
        _dbContext.StudyTopics.AddAsync(topic, cancellationToken).AsTask();

    public async Task<IReadOnlyList<Achievement>> ListCatalogAsync(CancellationToken cancellationToken) =>
        await _dbContext.Achievements.AsNoTracking().ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentAchievement>> ListEarnedAsync(Guid studentId, CancellationToken cancellationToken) =>
        await _dbContext.StudentAchievements.Where(x => x.StudentId == studentId).ToArrayAsync(cancellationToken);

    public Task AddEarnedAsync(StudentAchievement earned, CancellationToken cancellationToken) =>
        _dbContext.StudentAchievements.AddAsync(earned, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
