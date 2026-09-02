using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Settings.Domain;

public sealed class UserSetting : AggregateRoot<Guid>
{
    private UserSetting()
    {
    }

    public UserSetting(
        Guid id,
        Guid userId,
        bool pushNotificationsEnabled,
        bool emailNotificationsEnabled,
        bool upcomingLessonReminderEnabled,
        bool homeworkReminderEnabled,
        bool paymentReminderEnabled,
        bool weeklySummaryEnabled,
        bool shareStudyDataWithTeacher,
        bool shareStudyDataWithParent,
        PrivacyLevel privacyLevel,
        SessionTerminationPolicy sessionTerminationPolicy,
        DateTime lastUpdatedOnUtc)
    {
        Id = id;
        UserId = userId;
        PushNotificationsEnabled = pushNotificationsEnabled;
        EmailNotificationsEnabled = emailNotificationsEnabled;
        UpcomingLessonReminderEnabled = upcomingLessonReminderEnabled;
        HomeworkReminderEnabled = homeworkReminderEnabled;
        PaymentReminderEnabled = paymentReminderEnabled;
        WeeklySummaryEnabled = weeklySummaryEnabled;
        ShareStudyDataWithTeacher = shareStudyDataWithTeacher;
        ShareStudyDataWithParent = shareStudyDataWithParent;
        PrivacyLevel = privacyLevel;
        SessionTerminationPolicy = sessionTerminationPolicy;
        LastUpdatedOnUtc = lastUpdatedOnUtc;
    }

    public Guid UserId { get; private set; }

    public bool PushNotificationsEnabled { get; private set; }

    public bool EmailNotificationsEnabled { get; private set; }

    public bool UpcomingLessonReminderEnabled { get; private set; }

    public bool HomeworkReminderEnabled { get; private set; }

    public bool PaymentReminderEnabled { get; private set; }

    public bool WeeklySummaryEnabled { get; private set; }

    public bool ShareStudyDataWithTeacher { get; private set; }

    public bool ShareStudyDataWithParent { get; private set; }

    public PrivacyLevel PrivacyLevel { get; private set; }

    public SessionTerminationPolicy SessionTerminationPolicy { get; private set; }

    public DateTime LastUpdatedOnUtc { get; private set; }

    public void SetStudySharing(bool shareWithTeacher, bool shareWithParent, DateTime updatedOnUtc)
    {
        ShareStudyDataWithTeacher = shareWithTeacher;
        ShareStudyDataWithParent = shareWithParent;
        LastUpdatedOnUtc = updatedOnUtc;
    }

    /// <summary>Bildirim/gizlilik/güvenlik tercihlerini günceller. Çalışma paylaşımı ayrı uçtan yönetilir.</summary>
    /// <param name="pushNotificationsEnabled">Anlık bildirimler açık mı.</param>
    /// <param name="emailNotificationsEnabled">E-posta bildirimleri açık mı.</param>
    /// <param name="upcomingLessonReminderEnabled">Yaklaşan ders hatırlatması açık mı.</param>
    /// <param name="homeworkReminderEnabled">Ödev hatırlatması açık mı.</param>
    /// <param name="paymentReminderEnabled">Ödeme hatırlatması açık mı.</param>
    /// <param name="weeklySummaryEnabled">Haftalık özet açık mı.</param>
    /// <param name="privacyLevel">Çalışma verisi gizlilik seviyesi.</param>
    /// <param name="sessionTerminationPolicy">Girişte diğer oturumlara ne olacağı.</param>
    /// <param name="updatedOnUtc">Güncelleme zamanı (UTC).</param>
    public void UpdatePreferences(
        bool pushNotificationsEnabled,
        bool emailNotificationsEnabled,
        bool upcomingLessonReminderEnabled,
        bool homeworkReminderEnabled,
        bool paymentReminderEnabled,
        bool weeklySummaryEnabled,
        PrivacyLevel privacyLevel,
        SessionTerminationPolicy sessionTerminationPolicy,
        DateTime updatedOnUtc)
    {
        PushNotificationsEnabled = pushNotificationsEnabled;
        EmailNotificationsEnabled = emailNotificationsEnabled;
        UpcomingLessonReminderEnabled = upcomingLessonReminderEnabled;
        HomeworkReminderEnabled = homeworkReminderEnabled;
        PaymentReminderEnabled = paymentReminderEnabled;
        WeeklySummaryEnabled = weeklySummaryEnabled;
        PrivacyLevel = privacyLevel;
        SessionTerminationPolicy = sessionTerminationPolicy;
        LastUpdatedOnUtc = updatedOnUtc;
    }

    /// <summary>Kaydı olmayan kullanıcı için varsayılan ayar (kalıcı değil, yalnız okuma yanıtı).</summary>
    /// <param name="userId">Kullanıcı kimliği.</param>
    /// <param name="nowUtc">Şimdiki zaman (UTC).</param>
    /// <returns>Tüm bildirimleri açık, paylaşımı kapalı varsayılan ayar.</returns>
    public static UserSetting Default(Guid userId, DateTime nowUtc)
        => new(Guid.Empty, userId, true, true, true, true, true, true, false, false,
            PrivacyLevel.Standard, SessionTerminationPolicy.KeepLatest, nowUtc);
}

public enum PrivacyLevel
{
    Standard = 1,
    Limited = 2,
    Hidden = 3
}

public enum SessionTerminationPolicy
{
    KeepLatest = 1,
    TerminateOtherSessions = 2
}
