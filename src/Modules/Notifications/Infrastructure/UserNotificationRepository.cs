using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

internal sealed class UserNotificationRepository : IUserNotificationRepository
{
    private readonly NotificationsDbContext _dbContext;

    public UserNotificationRepository(NotificationsDbContext dbContext) => _dbContext = dbContext;

    public async Task<(IReadOnlyList<UserNotification> Items, int TotalCount)> ListAsync(
        Guid userId,
        bool onlyUnread,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.UserNotifications.Where(entity => entity.UserId == userId);
        if (onlyUnread)
        {
            query = query.Where(entity => entity.ReadOnUtc == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(entity => entity.CreatedOnUtc)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken)
        => _dbContext.UserNotifications.CountAsync(
            entity => entity.UserId == userId && entity.ReadOnUtc == null,
            cancellationToken);

    public Task<UserNotification?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken)
        => _dbContext.UserNotifications.FindAsync(new object?[] { notificationId }, cancellationToken).AsTask();

    public async Task<IReadOnlyList<UserNotification>> ListPendingPushAsync(int maxCount, CancellationToken cancellationToken)
        => await _dbContext.UserNotifications
            .Where(entity => entity.PushedOnUtc == null)
            .OrderBy(entity => entity.CreatedOnUtc)
            .Take(maxCount)
            .ToArrayAsync(cancellationToken);

    public Task AddAsync(UserNotification notification, CancellationToken cancellationToken)
        => _dbContext.UserNotifications.AddAsync(notification, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
