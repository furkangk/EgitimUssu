using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

internal sealed class ParentNotificationRepository : IParentNotificationRepository
{
    public async Task<int> RunWeeklySummaryBatchAsync(Func<Task<int>> write, CancellationToken cancellationToken)
    {
        var attempt = 0;
        return await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) _dbContext.ChangeTracker.Clear();
            await using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
            await AcquireWeeklySummaryLockAsync(_dbContext, cancellationToken);
            var created = await write();
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return created;
        });
    }

    // Birleşim de aynı transaction kilidini alır; okuma → tekilleştirme → yazım bölünemez.
    internal static Task AcquireWeeklySummaryLockAsync(NotificationsDbContext db, CancellationToken cancellationToken)
        => db.Database.IsNpgsql()
            ? db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('notifications:weekly-summary'))", cancellationToken)
            : Task.CompletedTask;

    private readonly NotificationsDbContext _dbContext;

    public ParentNotificationRepository(NotificationsDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(ParentNotification notification, CancellationToken cancellationToken)
        => _dbContext.ParentNotifications.AddAsync(notification, cancellationToken).AsTask();

    public async Task<IReadOnlyCollection<ParentNotification>> ListByParentAsync(Guid parentUserId, CancellationToken cancellationToken)
        => await _dbContext.ParentNotifications
            .Where(n => n.ParentUserId == parentUserId)
            .OrderByDescending(n => n.CreatedOnUtc)
            .ToArrayAsync(cancellationToken);

    public Task<bool> HasProcessedAsync(Guid eventId, CancellationToken cancellationToken)
        => _dbContext.ProcessedIntegrationEvents.AnyAsync(p => p.Id == eventId, cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetProcessedAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken)
        => (await _dbContext.ProcessedIntegrationEvents.Where(x => eventIds.Contains(x.Id))
            .Select(x => x.Id).ToArrayAsync(cancellationToken)).ToHashSet();

    public void MarkProcessed(Guid eventId, string eventName, DateTime nowUtc)
        => _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent(eventId, eventName, nowUtc));

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
