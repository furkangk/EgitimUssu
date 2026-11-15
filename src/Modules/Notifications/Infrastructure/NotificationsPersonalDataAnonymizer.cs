using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>
/// E-07: Silinen kullanıcının bildirim verisini siler.
/// <para>
/// 🔴 En kritik satır <b>cihaz jetonlarıdır</b>: silinmezse, hesabı silinmiş bir kişinin telefonuna
/// push gönderilmeye devam eder. Kullanıcının gördüğü en somut ihlal budur.
/// </para>
/// </summary>
public sealed class NotificationsPersonalDataAnonymizer : IPersonalDataAnonymizer
{
    private readonly NotificationsDbContext _dbContext;

    public NotificationsPersonalDataAnonymizer(NotificationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string ModuleName => "Notifications";

    public async Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var studentIds = subject.StudentIds.ToArray();

        var deviceTokens = await _dbContext.DeviceTokens
            .Where(token => token.UserId == subject.UserId)
            .ToArrayAsync(cancellationToken);

        var userNotifications = await _dbContext.UserNotifications
            .Where(notification => notification.UserId == subject.UserId)
            .ToArrayAsync(cancellationToken);

        var parentNotifications = await _dbContext.ParentNotifications
            .Where(notification => notification.ParentUserId == subject.UserId
                || studentIds.Contains(notification.StudentId))
            .ToArrayAsync(cancellationToken);

        var reminders = await _dbContext.LessonReminders
            .Where(reminder => reminder.TeacherUserId == subject.UserId || reminder.RecipientUserId == subject.UserId
                || studentIds.Contains(reminder.StudentId))
            .ToArrayAsync(cancellationToken);

        _dbContext.DeviceTokens.RemoveRange(deviceTokens);
        _dbContext.UserNotifications.RemoveRange(userNotifications);
        _dbContext.ParentNotifications.RemoveRange(parentNotifications);
        _dbContext.LessonReminders.RemoveRange(reminders);

        var affected = deviceTokens.Length + userNotifications.Length
            + parentNotifications.Length + reminders.Length;

        if (affected > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return affected;
    }
}
