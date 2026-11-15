using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Push;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P03 Task 3 (A-04/Y5): Zamanlayıcı artık gerçekten push gönderir. Kritik davranışlar:
/// gönderilemeyen hatırlatma <b>Sent işaretlenmez</b> (sessiz kayıp yok), geçersiz token kapatılır,
/// hiç aktif cihazı olmayan hatırlatma kuyruğu tıkamaz.
/// </summary>
public sealed class NotificationDispatchProcessorTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Due_Reminder_Should_Be_Sent_To_All_Active_Device_Tokens()
    {
        var reminder = CreateReminder(out var userId);
        var sender = new FakePushSender(PushDeliveryStatus.Delivered);
        var deviceTokens = new FakeDeviceTokenRepository(
            CreateToken(userId, "token-1"),
            CreateToken(userId, "token-2"));
        var processor = CreateProcessor(reminder, deviceTokens, sender);

        var sentCount = await processor.DispatchDueRemindersAsync();

        Assert.Equal(2, sender.SentTokens.Count);
        Assert.Equal(ReminderStatus.Sent, reminder.Status);
        Assert.Equal(1, sentCount);
    }

    [Fact]
    public async Task Push_Payload_Should_Carry_Deep_Link_Route()
    {
        var reminder = CreateReminder(out var userId);
        var sender = new FakePushSender(PushDeliveryStatus.Delivered);
        var processor = CreateProcessor(reminder, new FakeDeviceTokenRepository(CreateToken(userId, "token-1")), sender);

        await processor.DispatchDueRemindersAsync();

        var message = Assert.Single(sender.SentMessages);
        Assert.Equal(reminder.Title, message.Title);
        Assert.Equal("/scheduling", message.Data["route"]);
        Assert.Equal(reminder.LessonScheduleId.ToString(), message.Data["lessonId"]);
    }

    [Fact]
    public async Task Reminder_Should_Not_Be_Marked_Sent_When_Push_Fails()
    {
        var reminder = CreateReminder(out var userId);
        var sender = new FakePushSender(PushDeliveryStatus.TransientFailure, "503");
        var processor = CreateProcessor(reminder, new FakeDeviceTokenRepository(CreateToken(userId, "token-1")), sender);

        var sentCount = await processor.DispatchDueRemindersAsync();

        Assert.Equal(ReminderStatus.Pending, reminder.Status);
        Assert.Equal(1, reminder.AttemptCount);
        Assert.Equal("503", reminder.LastError);
        Assert.Equal(0, sentCount);
    }

    [Fact]
    public async Task Reminder_Should_Fail_Permanently_After_Five_Attempts()
    {
        var reminder = CreateReminder(out var userId);
        var sender = new FakePushSender(PushDeliveryStatus.TransientFailure, "503");
        var processor = CreateProcessor(reminder, new FakeDeviceTokenRepository(CreateToken(userId, "token-1")), sender);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await processor.DispatchDueRemindersAsync();
        }

        Assert.Equal(5, reminder.AttemptCount);
        Assert.Equal(ReminderStatus.Failed, reminder.Status);
    }

    [Fact]
    public async Task Invalid_Token_Should_Be_Deactivated()
    {
        var reminder = CreateReminder(out var userId);
        var token = CreateToken(userId, "eski-token");
        var sender = new FakePushSender(PushDeliveryStatus.TokenInvalid, "UNREGISTERED");
        var processor = CreateProcessor(reminder, new FakeDeviceTokenRepository(token), sender);

        await processor.DispatchDueRemindersAsync();

        Assert.False(token.IsActive);
        Assert.NotEqual(ReminderStatus.Sent, reminder.Status);
    }

    [Fact]
    public async Task Reminder_Without_Active_Token_Should_Be_Marked_Sent_Once()
    {
        var reminder = CreateReminder(out _);
        var sender = new FakePushSender(PushDeliveryStatus.Delivered);
        var processor = CreateProcessor(reminder, new FakeDeviceTokenRepository(), sender);

        var sentCount = await processor.DispatchDueRemindersAsync();

        Assert.Empty(sender.SentTokens);
        Assert.Equal(ReminderStatus.Sent, reminder.Status);
        Assert.Equal(1, sentCount);
    }

    [Fact]
    public async Task Pending_User_Notification_Should_Be_Pushed_And_Marked()
    {
        var userId = Guid.NewGuid();
        var notification = CreateUserNotification(userId);
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository(notification);
        var sender = new FakePushSender(PushDeliveryStatus.Delivered);
        var processor = CreateProcessor(
            CreateReminder(out _),
            new FakeDeviceTokenRepository(CreateToken(userId, "token-1")),
            sender,
            userNotifications);

        var pushedCount = await processor.DispatchPendingUserNotificationsAsync();

        Assert.Equal(1, pushedCount);
        Assert.Equal(Now, notification.PushedOnUtc);
        var message = Assert.Single(sender.SentMessages);
        Assert.Equal("/student/assignments", message.Data["route"]);
        Assert.Equal("AssignmentCreated", message.Data["kind"]);
    }

    [Fact]
    public async Task Failed_User_Notification_Push_Should_Stay_Pending()
    {
        var userId = Guid.NewGuid();
        var notification = CreateUserNotification(userId);
        var sender = new FakePushSender(PushDeliveryStatus.TransientFailure, "503");
        var processor = CreateProcessor(
            CreateReminder(out _),
            new FakeDeviceTokenRepository(CreateToken(userId, "token-1")),
            sender,
            new UserNotificationTests.FakeUserNotificationRepository(notification));

        var pushedCount = await processor.DispatchPendingUserNotificationsAsync();

        Assert.Equal(0, pushedCount);
        Assert.Null(notification.PushedOnUtc);
    }

    [Fact]
    public async Task User_Notification_Without_Device_Should_Not_Block_Queue()
    {
        var notification = CreateUserNotification(Guid.NewGuid());
        var sender = new FakePushSender(PushDeliveryStatus.Delivered);
        var processor = CreateProcessor(
            CreateReminder(out _),
            new FakeDeviceTokenRepository(),
            sender,
            new UserNotificationTests.FakeUserNotificationRepository(notification));

        await processor.DispatchPendingUserNotificationsAsync();

        Assert.Empty(sender.SentTokens);
        Assert.Equal(Now, notification.PushedOnUtc);
    }

    [Fact]
    public async Task Dispatch_uses_recipient_instead_of_teacher()
    {
        var recipient = Guid.NewGuid();
        var reminder = CreateReminder(out var teacher, recipient);
        var sender = new FakePushSender(PushDeliveryStatus.Delivered);
        await CreateProcessor(reminder, new FakeDeviceTokenRepository(CreateToken(teacher, "teacher"), CreateToken(recipient, "student")), sender).DispatchDueRemindersAsync();
        Assert.Equal(new[] { "student" }, sender.SentTokens);
    }

    [Theory]
    [InlineData(31, 60, true)]
    [InlineData(30, 60, false)]
    [InlineData(1, -1, true)]
    public async Task Stale_or_started_reminder_expires_without_push(int minutesLate, int minutesUntilStart, bool expired)
    {
        var user = Guid.NewGuid();
        var reminder = new LessonReminder(Guid.NewGuid(), Guid.NewGuid(), user, Guid.NewGuid(), "Ders", "Saat", Now.AddMinutes(minutesUntilStart), Now.AddMinutes(-minutesLate), NotificationChannel.Push, ReminderStatus.Pending, Now);
        var sender = new FakePushSender(PushDeliveryStatus.Delivered);
        await CreateProcessor(reminder, new FakeDeviceTokenRepository(CreateToken(user, "token")), sender).DispatchDueRemindersAsync();
        Assert.Equal(expired ? ReminderStatus.Expired : ReminderStatus.Sent, reminder.Status);
        Assert.Equal(expired ? 0 : 1, sender.SentTokens.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    public async Task Current_disabled_or_changed_offset_blocks_stale_trigger_before_outbox_delivery(int offset)
    {
        var reminder = CreateReminder(out var teacher);
        var sender = new FakePushSender(PushDeliveryStatus.Delivered);
        var reader = new LessonReminderPlannerTests.Reader { Items = { new(reminder.LessonScheduleId, teacher,
            reminder.StudentId, reminder.OccurrenceStartUtc, reminder.ScheduledLessonStartAtUtc, offset) } };
        await CreateProcessor(reminder, new FakeDeviceTokenRepository(CreateToken(teacher, "token")), sender, schedules: reader).DispatchDueRemindersAsync();
        Assert.Empty(sender.SentTokens);
        Assert.Equal(ReminderStatus.Cancelled, reminder.Status);
    }

    private static UserNotification CreateUserNotification(Guid userId)
        => new(
            Guid.NewGuid(),
            userId,
            UserNotificationKind.AssignmentCreated,
            "Yeni ödev",
            "Matematik ödevi eklendi",
            "/student/assignments",
            Now.AddMinutes(-5));

    private static NotificationDispatchProcessor CreateProcessor(
        LessonReminder reminder,
        FakeDeviceTokenRepository deviceTokens,
        FakePushSender sender,
        UserNotificationTests.FakeUserNotificationRepository? userNotifications = null,
        LessonReminderPlannerTests.Reader? schedules = null)
        => new(
            new FakeLessonReminderRepository(reminder),
            deviceTokens,
            userNotifications ?? new UserNotificationTests.FakeUserNotificationRepository(),
            sender,
            new AllEnabledPreferences(),
            new FixedClock(Now),
            new LessonReminderPlannerTests.Students { Owner = reminder.RecipientUserId },
            new LessonReminderPlannerTests.Parents(),
            schedules ?? new LessonReminderPlannerTests.Reader { Items = { new(reminder.LessonScheduleId, reminder.TeacherUserId,
                reminder.StudentId, reminder.OccurrenceStartUtc, reminder.ScheduledLessonStartAtUtc, (int)(reminder.ScheduledLessonStartAtUtc - reminder.RemindAtUtc).TotalMinutes) } });

    private static LessonReminder CreateReminder(out Guid teacherUserId, Guid? recipient = null)
    {
        teacherUserId = Guid.NewGuid();
        return new LessonReminder(
            Guid.NewGuid(),
            Guid.NewGuid(),
            teacherUserId,
            Guid.NewGuid(),
            "Yaklasan ders hatirlatmasi",
            "Ders 30 dakika sonra baslayacak.",
            Now.AddMinutes(30),
            Now.AddMinutes(-1),
            NotificationChannel.Push,
            ReminderStatus.Pending,
            Now.AddDays(-1), recipientUserId: recipient);
    }

    private static DeviceToken CreateToken(Guid userId, string token)
        => new(Guid.NewGuid(), userId, token, DevicePlatform.Android, "Pixel 8", Now.AddDays(-1));

    // M11-5: Bu testler tercih kapısını konu almaz; hepsi açık varsayılır.
    private sealed class AllEnabledPreferences : EgitimUssu.Shared.Contracts.IUserNotificationPreferences
    {
        public Task<IReadOnlyDictionary<Guid, EgitimUssu.Shared.Contracts.UserNotificationPreferences>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<Guid, EgitimUssu.Shared.Contracts.UserNotificationPreferences>>(userIds.Distinct().ToDictionary(x => x, _ => EgitimUssu.Shared.Contracts.UserNotificationPreferences.AllEnabled));

        public Task<EgitimUssu.Shared.Contracts.UserNotificationPreferences> GetAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(EgitimUssu.Shared.Contracts.UserNotificationPreferences.AllEnabled);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakePushSender(PushDeliveryStatus status, string? error = null) : IPushSender
    {
        public List<string> SentTokens { get; } = [];

        public List<PushMessage> SentMessages { get; } = [];

        public Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken = default)
        {
            SentTokens.Add(deviceToken);
            SentMessages.Add(message);
            return Task.FromResult(new PushResult(status, error));
        }
    }

    private sealed class FakeDeviceTokenRepository(params DeviceToken[] tokens) : IDeviceTokenRepository
    {
        private readonly List<DeviceToken> _tokens = [.. tokens];

        public int SaveChangesCallCount { get; private set; }

        public Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken)
            => Task.FromResult(_tokens.FirstOrDefault(t => t.Token == token));

        public Task<IReadOnlyList<DeviceToken>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DeviceToken>>(
                _tokens.Where(t => t.UserId == userId && t.IsActive).ToList());

        public Task<IReadOnlyList<DeviceToken>> ListActiveByUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DeviceToken>>(_tokens.Where(t => userIds.Contains(t.UserId) && t.IsActive).ToArray());

        public Task AddAsync(DeviceToken token, CancellationToken cancellationToken)
        {
            _tokens.Add(token);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLessonReminderRepository(params LessonReminder[] reminders) : ILessonReminderRepository
    {
        private readonly List<LessonReminder> _reminders = [.. reminders];

        public Task<LessonReminder?> GetByLessonScheduleIdAsync(Guid lessonScheduleId, CancellationToken cancellationToken)
            => Task.FromResult(_reminders.FirstOrDefault(r => r.LessonScheduleId == lessonScheduleId));

        public Task<IReadOnlyCollection<LessonReminder>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonReminder>>(
                _reminders.Where(r => r.TeacherUserId == teacherUserId).ToList());

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
