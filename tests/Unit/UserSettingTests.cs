using EgitimUssu.Modules.Settings.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class UserSettingTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    private static UserSetting New()
        => new(Guid.NewGuid(), Guid.NewGuid(),
            pushNotificationsEnabled: true, emailNotificationsEnabled: true,
            upcomingLessonReminderEnabled: true, homeworkReminderEnabled: true,
            paymentReminderEnabled: true, weeklySummaryEnabled: true,
            shareStudyDataWithTeacher: true, shareStudyDataWithParent: true,
            privacyLevel: PrivacyLevel.Standard,
            sessionTerminationPolicy: SessionTerminationPolicy.KeepLatest,
            lastUpdatedOnUtc: Now);

    [Fact]
    public void SetStudySharing_UpdatesFlagsAndTimestamp()
    {
        var s = New();
        var later = Now.AddMinutes(5);

        s.SetStudySharing(shareWithTeacher: false, shareWithParent: false, later);

        Assert.False(s.ShareStudyDataWithTeacher);
        Assert.False(s.ShareStudyDataWithParent);
        Assert.Equal(later, s.LastUpdatedOnUtc);
    }
}
