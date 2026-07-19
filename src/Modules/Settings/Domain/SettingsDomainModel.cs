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
