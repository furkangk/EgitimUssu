using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P16 T5 / N-50, N-06 (yalnız metin): bildirimlerde kullanıcıya gösterilen saat Türkiye saatidir. Eskiden UTC basılıyordu —
/// 23:59 TR son teslim "20:59", ayın 1'i 00:30 TR vade önceki günün tarihi, ders hatırlatması ISO UTC damgası olarak
/// görünüyordu. Hatırlatmanın <b>anı</b> ve oluşum mantığı P17 T6'dadır; burada yalnız metin biçimi.
/// </summary>
public sealed class NotificationLocalTimeTextTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 20, 59, 0, DateTimeKind.Utc);
    private static readonly LocalCalendar Istanbul = new("Europe/Istanbul", new FixedClock(Now));

    [Fact]
    public void Date_time_text_is_turkey_time()
        => Assert.Equal("20.11.2026 23:59", NotificationTimeText.LocalDateTime(Istanbul, new DateTime(2026, 11, 20, 20, 59, 0, DateTimeKind.Utc)));

    [Fact]
    public void Date_text_is_the_turkey_day()
        => Assert.Equal("01.09.2026", NotificationTimeText.LocalDate(Istanbul, new DateTime(2026, 8, 31, 21, 30, 0, DateTimeKind.Utc)));

    [Fact]
    public void Lesson_reminder_body_shows_the_local_day_and_hour()
    {
        var body = NotificationTimeText.LessonReminderBody(Istanbul, new DateTime(2026, 11, 13, 7, 0, 0, DateTimeKind.Utc));

        Assert.Contains("13 Kasım Cuma 10:00", body);
        Assert.DoesNotContain("T07:00", body);
    }

    [Fact]
    public async Task Assignment_due_soon_notification_shows_2359_for_a_2359_deadline()
    {
        var studentId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var students = new FakeMessagingStudentDirectory();
        students.Owners[studentId] = studentUserId;
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository();
        await using var dbContext = NewDbContext();
        var handler = new AssignmentDueSoonNotificationHandler(
            dbContext, students, userNotifications, new SequentialIds(), new FixedClock(Now), Istanbul);

        await handler.HandleAsync(new IntegrationEvent(
            Guid.NewGuid(), Now, "AssignmentDueSoonDomainEvent", "Assignments",
            $$"""{"assignmentId":"{{Guid.NewGuid()}}","studentId":"{{studentId}}","teacherUserId":"{{Guid.NewGuid()}}","title":"Türev föyü","dueDateUtc":"2026-11-20T20:59:00Z","notifiedOnUtc":"2026-11-19T20:59:00Z"}"""));

        var notification = Assert.Single(await userNotifications.ListPendingPushAsync(10, default));
        Assert.Equal(studentUserId, notification.UserId);
        Assert.Contains("20.11.2026 23:59", notification.Body);
        Assert.DoesNotContain("20:59", notification.Body);
    }

    [Fact]
    public async Task Payment_overdue_notification_shows_the_turkey_due_day()
    {
        var teacherUserId = Guid.NewGuid();
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository();
        await using var dbContext = NewDbContext();
        var handler = new PaymentOverdueNotificationHandler(
            dbContext, new NoParents(), userNotifications, new SequentialIds(), new FixedClock(Now), Istanbul);

        // Vade 1 Eylül 00:30 TR = 31 Ağustos 21:30Z.
        await handler.HandleAsync(new IntegrationEvent(
            Guid.NewGuid(), Now, "PaymentBecameOverdueDomainEvent", "Payments",
            $$"""{"paymentRecordId":"{{Guid.Empty}}","teacherUserId":"{{teacherUserId}}","studentId":"{{Guid.NewGuid()}}","outstandingAmount":250.0,"currency":"TRY","dueDateUtc":"2026-08-31T21:30:00Z","markedOnUtc":"2026-09-04T12:00:00Z"}"""));

        var notification = Assert.Single(await userNotifications.ListPendingPushAsync(10, default));
        Assert.StartsWith("01.09.2026 vadeli", notification.Body);
    }

    private static NotificationsDbContext NewDbContext()
        => new(
            new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseInMemoryDatabase($"notification-text-{Guid.NewGuid():N}")
                .Options,
            new NoOpDomainEventMapper());

    private sealed class NoParents : IParentNotificationDirectory
    {
        public Task<ParentNotificationTargetPage> ListApprovedTargetPageAsync(Guid? afterLinkId, int take, CancellationToken cancellationToken)
            => Task.FromResult(new ParentNotificationTargetPage(Array.Empty<ParentStudentNotificationTarget>(), null));

        public Task<IReadOnlyCollection<ParentNotificationTarget>> GetApprovedParentsForStudentAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ParentNotificationTarget>>([]);

        public Task<IReadOnlyCollection<ParentStudentNotificationTarget>> ListAllApprovedTargetsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ParentStudentNotificationTarget>>([]);
    }

    private sealed class SequentialIds : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
