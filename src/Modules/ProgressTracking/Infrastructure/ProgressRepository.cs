using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Modules.ProgressTracking.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

internal sealed class ProgressRepository : IProgressRepository
{
    private readonly ProgressTrackingDbContext _dbContext;

    public ProgressRepository(ProgressTrackingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<TopicMastery?> GetMasteryAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken) =>
        _dbContext.TopicMasteries.FirstOrDefaultAsync(
            x => x.StudentId == studentId && x.Subject == subject && x.Topic == topic, cancellationToken);

    public async Task<IReadOnlyList<TopicMastery>> ListMasteryAsync(Guid studentId, string? subject, CancellationToken cancellationToken)
    {
        var query = _dbContext.TopicMasteries.Where(x => x.StudentId == studentId);
        if (!string.IsNullOrWhiteSpace(subject))
        {
            query = query.Where(x => x.Subject == subject);
        }

        return await query.ToArrayAsync(cancellationToken);
    }

    public Task AddMasteryAsync(TopicMastery mastery, CancellationToken cancellationToken) =>
        _dbContext.TopicMasteries.AddAsync(mastery, cancellationToken).AsTask();

    public Task<TopicGoal?> GetGoalAsync(Guid goalId, CancellationToken cancellationToken) =>
        _dbContext.TopicGoals.FirstOrDefaultAsync(x => x.Id == goalId, cancellationToken);

    public async Task<IReadOnlyList<TopicGoal>> ListGoalsAsync(Guid studentId, TopicGoalStatus? status, CancellationToken cancellationToken)
    {
        var query = _dbContext.TopicGoals.Where(x => x.StudentId == studentId);
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query.OrderByDescending(x => x.CreatedOnUtc).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TopicGoal>> ListActiveGoalsForTopicAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken) =>
        await _dbContext.TopicGoals
            .Where(x => x.StudentId == studentId && x.Subject == subject && x.Topic == topic && x.Status == TopicGoalStatus.Active)
            .ToArrayAsync(cancellationToken);

    public Task AddGoalAsync(TopicGoal goal, CancellationToken cancellationToken) =>
        _dbContext.TopicGoals.AddAsync(goal, cancellationToken).AsTask();

    public Task<bool> HasProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        _dbContext.ProcessedEvents.AnyAsync(x => x.Id == eventId, cancellationToken);

    public Task AddProcessedAsync(ProcessedEvent processed, CancellationToken cancellationToken) =>
        _dbContext.ProcessedEvents.AddAsync(processed, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
