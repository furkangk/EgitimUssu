using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Settings.Infrastructure;

/// <summary>
/// E-07: Kullanıcının ayar satırını siler. Ayarlar yalnız o kullanıcı içindir ve hesabı
/// kapandığında saklanmalarının hiçbir amacı kalmaz.
/// </summary>
public sealed class SettingsPersonalDataAnonymizer : IPersonalDataAnonymizer
{
    private readonly SettingsDbContext _dbContext;

    public SettingsPersonalDataAnonymizer(SettingsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string ModuleName => "Settings";

    public async Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.UserSettings
            .Where(setting => setting.UserId == subject.UserId)
            .ToArrayAsync(cancellationToken);

        if (settings.Length == 0)
        {
            return 0;
        }

        _dbContext.UserSettings.RemoveRange(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return settings.Length;
    }
}
