using EgitimUssu.Modules.Settings.Application;
using EgitimUssu.Modules.Settings.Domain;
using EgitimUssu.Shared.Kernel;

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

    [Fact]
    public void UpdatePreferences_Should_Change_Flags_And_Timestamp()
    {
        var setting = new UserSetting(
            Guid.NewGuid(), Guid.NewGuid(), true, true, true, true, true, true, false, false,
            PrivacyLevel.Standard, SessionTerminationPolicy.KeepLatest, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        setting.UpdatePreferences(
            pushNotificationsEnabled: false,
            emailNotificationsEnabled: true,
            upcomingLessonReminderEnabled: false,
            homeworkReminderEnabled: true,
            paymentReminderEnabled: false,
            weeklySummaryEnabled: true,
            privacyLevel: PrivacyLevel.Limited,
            sessionTerminationPolicy: SessionTerminationPolicy.TerminateOtherSessions,
            updatedOnUtc: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(setting.PushNotificationsEnabled);
        Assert.False(setting.UpcomingLessonReminderEnabled);
        Assert.Equal(PrivacyLevel.Limited, setting.PrivacyLevel);
        Assert.Equal(SessionTerminationPolicy.TerminateOtherSessions, setting.SessionTerminationPolicy);
        Assert.Equal(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), setting.LastUpdatedOnUtc);
        // Çalışma paylaşımı bu komutla değişmez (ayrı uç):
        Assert.False(setting.ShareStudyDataWithTeacher);
    }

    [Fact]
    public void Default_Should_Return_All_Enabled_Standard_Preferences()
    {
        var userId = Guid.NewGuid();

        var setting = UserSetting.Default(userId, Now);

        Assert.Equal(userId, setting.UserId);
        Assert.True(setting.PushNotificationsEnabled);
        Assert.True(setting.EmailNotificationsEnabled);
        Assert.True(setting.UpcomingLessonReminderEnabled);
        Assert.True(setting.HomeworkReminderEnabled);
        Assert.True(setting.PaymentReminderEnabled);
        Assert.True(setting.WeeklySummaryEnabled);
        Assert.False(setting.ShareStudyDataWithTeacher);
        Assert.False(setting.ShareStudyDataWithParent);
        Assert.Equal(PrivacyLevel.Standard, setting.PrivacyLevel);
        Assert.Equal(SessionTerminationPolicy.KeepLatest, setting.SessionTerminationPolicy);
    }

    // --- Handler'lar ---

    [Fact]
    public async Task GetUserSettings_Should_Return_Defaults_When_No_Record()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeUserSettingRepository();
        var handler = new GetUserSettingsQueryHandler(repository, new FakeClock(Now));

        var result = await handler.Handle(new GetUserSettingsQuery(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.UserId);
        Assert.True(result.Value.HomeworkReminderEnabled);
        Assert.Equal("Standard", result.Value.PrivacyLevel);
        Assert.Equal("KeepLatest", result.Value.SessionTerminationPolicy);
    }

    [Fact]
    public async Task UpdateUserSettings_Should_Create_Record_When_Missing_And_Preserve_Sharing()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeUserSettingRepository();
        var handler = new UpdateUserSettingsCommandHandler(repository, new FakeIdGenerator(), new FakeClock(Now));

        var result = await handler.Handle(
            new UpdateUserSettingsCommand(userId, false, true, false, true, false, true,
                PrivacyLevel.Hidden, SessionTerminationPolicy.TerminateOtherSessions),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(repository.Settings);
        Assert.False(stored.PushNotificationsEnabled);
        Assert.Equal(PrivacyLevel.Hidden, stored.PrivacyLevel);
        Assert.False(stored.ShareStudyDataWithTeacher);
        Assert.Equal("Hidden", result.Value.PrivacyLevel);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task UpdateUserSettings_Should_Update_Existing_Without_Touching_Study_Sharing()
    {
        var userId = Guid.NewGuid();
        var existing = new UserSetting(
            Guid.NewGuid(), userId, true, true, true, true, true, true,
            shareStudyDataWithTeacher: true, shareStudyDataWithParent: true,
            PrivacyLevel.Standard, SessionTerminationPolicy.KeepLatest, Now.AddDays(-1));
        var repository = new FakeUserSettingRepository();
        repository.Settings.Add(existing);
        var handler = new UpdateUserSettingsCommandHandler(repository, new FakeIdGenerator(), new FakeClock(Now));

        var result = await handler.Handle(
            new UpdateUserSettingsCommand(userId, true, false, true, false, true, false,
                PrivacyLevel.Limited, SessionTerminationPolicy.KeepLatest),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(repository.Settings);
        Assert.False(existing.EmailNotificationsEnabled);
        Assert.Equal(PrivacyLevel.Limited, existing.PrivacyLevel);
        Assert.True(existing.ShareStudyDataWithTeacher);
        Assert.True(existing.ShareStudyDataWithParent);
        Assert.Equal(Now, existing.LastUpdatedOnUtc);
    }

    // --- Authorizer'lar (M15-2) ---

    [Fact]
    public async Task Get_Authorizer_Should_Allow_Owner()
    {
        var userId = Guid.NewGuid();
        var authorizer = new GetUserSettingsQueryAuthorizer(new FakeCurrentUser(userId.ToString()));

        var result = await authorizer.Authorize(new GetUserSettingsQuery(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Get_Authorizer_Should_Forbid_Other_User()
    {
        var authorizer = new GetUserSettingsQueryAuthorizer(new FakeCurrentUser(Guid.NewGuid().ToString()));

        var result = await authorizer.Authorize(new GetUserSettingsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Get_Authorizer_Should_Allow_Admin()
    {
        var authorizer = new GetUserSettingsQueryAuthorizer(new FakeCurrentUser(Guid.NewGuid().ToString(), "Admin"));

        var result = await authorizer.Authorize(new GetUserSettingsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Get_Authorizer_Should_Forbid_Anonymous()
    {
        var authorizer = new GetUserSettingsQueryAuthorizer(new FakeCurrentUser(null));

        var result = await authorizer.Authorize(new GetUserSettingsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Update_Authorizer_Should_Forbid_Other_User()
    {
        var authorizer = new UpdateUserSettingsCommandAuthorizer(new FakeCurrentUser(Guid.NewGuid().ToString()));

        var result = await authorizer.Authorize(
            new UpdateUserSettingsCommand(Guid.NewGuid(), true, true, true, true, true, true,
                PrivacyLevel.Standard, SessionTerminationPolicy.KeepLatest),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Update_Authorizer_Should_Allow_Owner()
    {
        var userId = Guid.NewGuid();
        var authorizer = new UpdateUserSettingsCommandAuthorizer(new FakeCurrentUser(userId.ToString()));

        var result = await authorizer.Authorize(
            new UpdateUserSettingsCommand(userId, true, true, true, true, true, true,
                PrivacyLevel.Standard, SessionTerminationPolicy.KeepLatest),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private sealed class FakeUserSettingRepository : IUserSettingRepository
    {
        public List<UserSetting> Settings { get; } = [];
        public int SaveCount { get; private set; }

        public Task<UserSetting?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(Settings.FirstOrDefault(s => s.UserId == userId));

        public Task AddAsync(UserSetting setting, CancellationToken cancellationToken)
        {
            Settings.Add(setting);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }

    private sealed class FakeIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FakeCurrentUser(string? userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsAuthenticated => UserId is not null;
    }
}
