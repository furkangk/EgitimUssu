using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Settings.Infrastructure;

/// <summary>
/// M11-5: Settings'in bildirim tercihi sözleşmesini uygulaması. Notifications bu arayüz üzerinden
/// okur; Settings DbContext'ine doğrudan erişmez (modül sınırı korunur).
/// </summary>
internal sealed class UserNotificationPreferencesDirectory : IUserNotificationPreferences
{
    private readonly SettingsDbContext _dbContext;

    public UserNotificationPreferencesDirectory(SettingsDbContext dbContext) => _dbContext = dbContext;

    public async Task<UserNotificationPreferences> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await _dbContext.UserSettings
            .Where(s => s.UserId == userId)
            .Select(s => new
            {
                s.PushNotificationsEnabled,
                s.EmailNotificationsEnabled,
                s.UpcomingLessonReminderEnabled,
                s.HomeworkReminderEnabled,
                s.PaymentReminderEnabled,
                s.WeeklySummaryEnabled
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Kayıt yoksa kullanıcı hiçbir şeyi kapatmamıştır → hepsi açık.
        return row is null
            ? UserNotificationPreferences.AllEnabled
            : new UserNotificationPreferences(
                row.PushNotificationsEnabled,
                row.EmailNotificationsEnabled,
                row.UpcomingLessonReminderEnabled,
                row.HomeworkReminderEnabled,
                row.PaymentReminderEnabled,
                row.WeeklySummaryEnabled);
    }
}
