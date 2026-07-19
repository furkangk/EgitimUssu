using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

internal sealed class ParentNotificationRepository : IParentNotificationRepository
{
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

    public void MarkProcessed(Guid eventId, string eventName, DateTime nowUtc)
        => _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent(eventId, eventName, nowUtc));

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
