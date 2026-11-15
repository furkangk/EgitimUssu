using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Push;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// M11-5: Kullanıcının kapattığı bildirim türü gönderim yolunda gerçekten durdurulur.
/// Kapı <b>gönderim turunda</b> uygulanır (tek nokta) — böylece hatırlatma da, in-app kaynaklı
/// push da aynı tercihe uyar ve olay tüketicisine çapraz-modül okuma eklenmez (Öğrenilenler #40).
/// </summary>
public sealed class NotificationPreferenceGateTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Lesson_Reminder_Should_Not_Be_Sent_When_Preference_Disabled()
    {
        var reminder = CreateReminder(out var userId);
        var sender = new FakePushSender();
        var processor = CreateProcessor(
            reminder,
            new FakeDeviceTokenRepository(CreateToken(userId, "token-1")),
            sender,
            preferences: Prefs(lessonReminderEnabled: false));

        var sentCount = await processor.DispatchDueRemindersAsync();

        Assert.Empty(sender.SentTokens);
        // Kuyruk tıkanmasın: gönderilmiş sayılır, tekrar denenmez.
        Assert.Equal(ReminderStatus.Sent, reminder.Status);
        Assert.Equal(1, sentCount);
    }

    [Fact]
    public async Task Lesson_Reminder_Should_Not_Be_Sent_When_Push_Disabled()
    {
        var reminder = CreateReminder(out var userId);
        var sender = new FakePushSender();
        var processor = CreateProcessor(
            reminder,
            new FakeDeviceTokenRepository(CreateToken(userId, "token-1")),
            sender,
            preferences: Prefs(pushEnabled: false));

        await processor.DispatchDueRemindersAsync();

        Assert.Empty(sender.SentTokens);
        Assert.Equal(ReminderStatus.Sent, reminder.Status);
    }

    [Fact]
    public async Task Lesson_Reminder_Should_Be_Sent_When_Preference_Enabled()
    {
        var reminder = CreateReminder(out var userId);
        var sender = new FakePushSender();
        var processor = CreateProcessor(
            reminder,
            new FakeDeviceTokenRepository(CreateToken(userId, "token-1")),
            sender,
            preferences: Prefs());

        await processor.DispatchDueRemindersAsync();

        Assert.Single(sender.SentTokens);
        Assert.Equal(ReminderStatus.Sent, reminder.Status);
    }

    [Fact]
    public async Task Push_Disabled_Should_Skip_Push_But_Keep_InApp_Notification()
    {
        var userId = Guid.NewGuid();
        var notification = CreateUserNotification(userId, UserNotificationKind.AssignmentCreated);
        var repository = new UserNotificationTests.FakeUserNotificationRepository(notification);
        var sender = new FakePushSender();
        var processor = CreateProcessor(
            CreateReminder(out _),
            new FakeDeviceTokenRepository(CreateToken(userId, "token-1")),
            sender,
            repository,
            Prefs(pushEnabled: false));

        var pushedCount = await processor.DispatchPendingUserNotificationsAsync();

        Assert.Empty(sender.SentTokens);
        Assert.Equal(0, pushedCount);
        // In-app kaydı silinmez; yalnız push atlanır ve kuyruk tıkanmaz.
        Assert.Equal(Now, notification.PushedOnUtc);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Homework_Notification_Should_Be_Skipped_When_Homework_Reminder_Disabled()
    {
        var userId = Guid.NewGuid();
        var notification = CreateUserNotification(userId, UserNotificationKind.AssignmentCreated);
        var sender = new FakePushSender();
        var processor = CreateProcessor(
            CreateReminder(out _),
            new FakeDeviceTokenRepository(CreateToken(userId, "token-1")),
            sender,
            new UserNotificationTests.FakeUserNotificationRepository(notification),
            Prefs(homeworkReminderEnabled: false));

        await processor.DispatchPendingUserNotificationsAsync();

        Assert.Empty(sender.SentTokens);
        Assert.Equal(Now, notification.PushedOnUtc);
    }

    [Fact]
    public async Task Payment_Notification_Should_Be_Skipped_When_Payment_Reminder_Disabled()
    {
        var userId = Guid.NewGuid();
        var notification = CreateUserNotification(userId, UserNotificationKind.PaymentDue);
        var sender = new FakePushSender();
        var processor = CreateProcessor(
            CreateReminder(out _),
            new FakeDeviceTokenRepository(CreateToken(userId, "token-1")),
            sender,
            new UserNotificationTests.FakeUserNotificationRepository(notification),
            Prefs(paymentReminderEnabled: false));

        await processor.DispatchPendingUserNotificationsAsync();

        Assert.Empty(sender.SentTokens);
    }

    [Fact]
    public async Task System_Notification_Should_Ignore_Type_Preferences()
    {
        var userId = Guid.NewGuid();
        var notification = CreateUserNotification(userId, UserNotificationKind.System);
        var sender = new FakePushSender();
        var processor = CreateProcessor(
            CreateReminder(out _),
            new FakeDeviceTokenRepository(CreateToken(userId, "token-1")),
            sender,
            new UserNotificationTests.FakeUserNotificationRepository(notification),
            Prefs(homeworkReminderEnabled: false, paymentReminderEnabled: false, lessonReminderEnabled: false));

        var pushedCount = await processor.DispatchPendingUserNotificationsAsync();

        Assert.Single(sender.SentTokens);
        Assert.Equal(1, pushedCount);
    }

    private static UserNotificationPreferences Prefs(
        bool pushEnabled = true,
        bool lessonReminderEnabled = true,
        bool homeworkReminderEnabled = true,
        bool paymentReminderEnabled = true)
        => new(pushEnabled, true, lessonReminderEnabled, homeworkReminderEnabled, paymentReminderEnabled, true);

    private static NotificationDispatchProcessor CreateProcessor(
        LessonReminder reminder,
        FakeDeviceTokenRepository deviceTokens,
        FakePushSender sender,
        UserNotificationTests.FakeUserNotificationRepository? userNotifications = null,
        UserNotificationPreferences? preferences = null)
        => new(
            new FakeLessonReminderRepository(reminder),
            deviceTokens,
            userNotifications ?? new UserNotificationTests.FakeUserNotificationRepository(),
            sender,
            new FakeUserNotificationPreferences(preferences ?? UserNotificationPreferences.AllEnabled),
            new FixedClock(Now),
            new LessonReminderPlannerTests.Students { Owner = reminder.RecipientUserId },
            new LessonReminderPlannerTests.Parents(),
            new LessonReminderPlannerTests.Reader { Items = { new(reminder.LessonScheduleId, reminder.TeacherUserId,
                reminder.StudentId, reminder.OccurrenceStartUtc, reminder.ScheduledLessonStartAtUtc, (int)(reminder.ScheduledLessonStartAtUtc - reminder.RemindAtUtc).TotalMinutes) } });

    private static UserNotification CreateUserNotification(Guid userId, UserNotificationKind kind)
        => new(Guid.NewGuid(), userId, kind, "Baslik", "Govde", "/route", Now.AddMinutes(-5));

    private static LessonReminder CreateReminder(out Guid teacherUserId)
    {
        teacherUserId = Guid.NewGuid();
        return new LessonReminder(
            Guid.NewGuid(), Guid.NewGuid(), teacherUserId, Guid.NewGuid(),
            "Yaklasan ders", "Ders 30 dakika sonra.",
            Now.AddMinutes(30), Now.AddMinutes(-1),
            NotificationChannel.Push, ReminderStatus.Pending, Now.AddDays(-1));
    }

    private static DeviceToken CreateToken(Guid userId, string token)
        => new(Guid.NewGuid(), userId, token, DevicePlatform.Android, "Pixel 8", Now.AddDays(-1));

    private sealed class FakeUserNotificationPreferences(UserNotificationPreferences preferences) : IUserNotificationPreferences
    {
        public Task<IReadOnlyDictionary<Guid, EgitimUssu.Shared.Contracts.UserNotificationPreferences>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<Guid, EgitimUssu.Shared.Contracts.UserNotificationPreferences>>(userIds.Distinct().ToDictionary(x => x, _ => preferences));

        public Task<UserNotificationPreferences> GetAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(preferences);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakePushSender : IPushSender
    {
        public List<string> SentTokens { get; } = [];

        public Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken = default)
        {
            SentTokens.Add(deviceToken);
            return Task.FromResult(new PushResult(PushDeliveryStatus.Delivered, null));
        }
    }

    private sealed class FakeDeviceTokenRepository(params DeviceToken[] tokens) : IDeviceTokenRepository
    {
        private readonly List<DeviceToken> _tokens = [.. tokens];

        public Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken)
            => Task.FromResult(_tokens.FirstOrDefault(t => t.Token == token));

        public Task<IReadOnlyList<DeviceToken>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DeviceToken>>(_tokens.Where(t => t.UserId == userId && t.IsActive).ToList());

        public Task<IReadOnlyList<DeviceToken>> ListActiveByUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DeviceToken>>(_tokens.Where(t => userIds.Contains(t.UserId) && t.IsActive).ToArray());

        public Task AddAsync(DeviceToken token, CancellationToken cancellationToken)
        {
            _tokens.Add(token);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeLessonReminderRepository(params LessonReminder[] reminders) : ILessonReminderRepository
    {
        private readonly List<LessonReminder> _reminders = [.. reminders];

        public Task<LessonReminder?> GetByLessonScheduleIdAsync(Guid lessonScheduleId, CancellationToken cancellationToken)
            => Task.FromResult(_reminders.FirstOrDefault(r => r.LessonScheduleId == lessonScheduleId));

        public Task<IReadOnlyCollection<LessonReminder>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonReminder>>(_reminders.Where(r => r.TeacherUserId == teacherUserId).ToList());

        public Task<IReadOnlyCollection<LessonReminder>> ListDuePendingAsync(DateTime utcNow, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonReminder>>(
                _reminders.Where(r => r.Status == ReminderStatus.Pending && r.RemindAtUtc <= utcNow).ToList());

        public Task AddAsync(LessonReminder reminder, CancellationToken cancellationToken)
        {
            _reminders.Add(reminder);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
