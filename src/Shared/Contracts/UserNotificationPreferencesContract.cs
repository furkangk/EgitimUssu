namespace EgitimUssu.Shared.Contracts;

// Modüller-arası salt-okunur bildirim tercihi sözleşmesi (M11-5).
// Settings uygular (UserSetting kaydından okur); Notifications tüketir — böylece kullanıcının
// kapattığı bir bildirim türü gönderim yolunda GERÇEKTEN durdurulur.
public sealed record UserNotificationPreferences(
    bool PushEnabled,
    bool EmailEnabled,
    bool LessonReminderEnabled,
    bool HomeworkReminderEnabled,
    bool PaymentReminderEnabled,
    bool WeeklySummaryEnabled)
{
    // Ayar kaydı olmayan kullanıcı için güvenli varsayılan: her şey açık (kullanıcı henüz kısıtlamadı).
    public static UserNotificationPreferences AllEnabled { get; } = new(true, true, true, true, true, true);
}

public interface IUserNotificationPreferences
{
    // userId: bildirimin alıcısı. Ayar kaydı yoksa AllEnabled döner.
    Task<UserNotificationPreferences> GetAsync(Guid userId, CancellationToken cancellationToken);
}
