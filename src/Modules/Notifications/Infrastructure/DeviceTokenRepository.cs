using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

internal sealed class DeviceTokenRepository : IDeviceTokenRepository
{
    private readonly NotificationsDbContext _dbContext;

    public DeviceTokenRepository(NotificationsDbContext dbContext) => _dbContext = dbContext;

    public Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken)
        => _dbContext.DeviceTokens.FirstOrDefaultAsync(entity => entity.Token == token, cancellationToken);

    public async Task<IReadOnlyList<DeviceToken>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken)
        => await _dbContext.DeviceTokens
            .Where(entity => entity.UserId == userId && entity.IsActive)
            .OrderByDescending(entity => entity.LastSeenOnUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<DeviceToken>> ListActiveByUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
        => await _dbContext.DeviceTokens.Where(t => userIds.Contains(t.UserId) && t.IsActive).ToArrayAsync(cancellationToken);

    public Task AddAsync(DeviceToken deviceToken, CancellationToken cancellationToken)
        => _dbContext.DeviceTokens.AddAsync(deviceToken, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
