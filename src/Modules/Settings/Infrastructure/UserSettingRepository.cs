using EgitimUssu.Modules.Settings.Application;
using EgitimUssu.Modules.Settings.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Settings.Infrastructure;

internal sealed class UserSettingRepository : IUserSettingRepository
{
    private readonly SettingsDbContext _dbContext;

    public UserSettingRepository(SettingsDbContext dbContext) => _dbContext = dbContext;

    public Task<UserSetting?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        => _dbContext.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

    public Task AddAsync(UserSetting setting, CancellationToken cancellationToken)
        => _dbContext.UserSettings.AddAsync(setting, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
