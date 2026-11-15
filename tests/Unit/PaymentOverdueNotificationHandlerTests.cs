using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgitimUssu.Tests.Unit;

/// <summary>M07-1 → M11: Vadesi geçen ödeme öğretmene (ve Premium veliye) in-app bildirim üretir.</summary>
public sealed class PaymentOverdueNotificationHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Overdue_Payment_Should_Notify_Teacher()
    {
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository();
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, userNotifications, parents: []);

        await handler.HandleAsync(Envelope(Guid.NewGuid()));

        var notification = Assert.Single(await userNotifications.ListPendingPushAsync(10, default));
        Assert.Equal(TeacherUserId, notification.UserId);
        Assert.Equal(UserNotificationKind.PaymentOverdue, notification.Kind);
        Assert.Equal("/payments", notification.Route);
        Assert.Null(notification.PushedOnUtc);
    }

    [Fact]
    public async Task Same_Event_Twice_Should_Notify_Once()
    {
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository();
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, userNotifications, parents: []);
        var envelope = Envelope(Guid.NewGuid());

        await handler.HandleAsync(envelope);
        await handler.HandleAsync(envelope);

        Assert.Single(await userNotifications.ListPendingPushAsync(10, default));
    }

    [Fact]
    public async Task Premium_Parent_With_Payment_Preference_Should_Also_Be_Notified()
    {
        var parentUserId = Guid.NewGuid();
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository();
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, userNotifications, parents:
        [
            new ParentNotificationTarget(parentUserId, MembershipTier.Premium, Prefs(payments: true))
        ]);

        await handler.HandleAsync(Envelope(Guid.NewGuid()));

        var created = await userNotifications.ListPendingPushAsync(10, default);
        Assert.Equal(2, created.Count);
        Assert.Contains(created, notification => notification.UserId == parentUserId && notification.Route == "/parent/payments");
    }

    [Fact]
    public async Task Free_Or_Preference_Off_Parent_Should_Not_Be_Notified()
    {
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository();
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, userNotifications, parents:
        [
            new ParentNotificationTarget(Guid.NewGuid(), MembershipTier.Free, Prefs(payments: true)),
            new ParentNotificationTarget(Guid.NewGuid(), MembershipTier.Premium, Prefs(payments: false))
        ]);

        await handler.HandleAsync(Envelope(Guid.NewGuid()));

        var notification = Assert.Single(await userNotifications.ListPendingPushAsync(10, default));
        Assert.Equal(TeacherUserId, notification.UserId);
    }

    private static PaymentOverdueNotificationHandler NewHandler(
        NotificationsDbContext dbContext,
        IUserNotificationRepository userNotifications,
        IReadOnlyCollection<ParentNotificationTarget> parents) =>
        new(
            dbContext,
            new FakeParentDirectory(parents),
            userNotifications,
            new SequentialIdGenerator(),
            new FixedClock(Now),
            new LocalCalendar("Europe/Istanbul", new FixedClock(Now)));

    private static NotificationsDbContext NewDbContext() =>
        new(
            new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new NoOpDomainEventMapper());

    private static ParentNotificationPrefs Prefs(bool payments) =>
        new(MissedAssignment: true, WeeklyProgressSummary: true, LessonReminders: true, TestResults: true, Payments: payments);

    private static IntegrationEvent Envelope(Guid eventId) =>
        new(
            eventId,
            Now,
            "PaymentBecameOverdueDomainEvent",
            "Payments",
            $$"""{"paymentRecordId":"{{Guid.Empty}}","teacherUserId":"{{TeacherUserId}}","studentId":"{{StudentId}}","outstandingAmount":250.0,"currency":"TRY","dueDateUtc":"2026-09-01T00:00:00Z","markedOnUtc":"2026-09-04T12:00:00Z"}""");

    private sealed class FakeParentDirectory : IParentNotificationDirectory
    {
        private readonly IReadOnlyCollection<ParentNotificationTarget> _targets;

        public FakeParentDirectory(IReadOnlyCollection<ParentNotificationTarget> targets) => _targets = targets;

        public Task<ParentNotificationTargetPage> ListApprovedTargetPageAsync(Guid? afterLinkId, int take, CancellationToken cancellationToken)
            => Task.FromResult(new ParentNotificationTargetPage(Array.Empty<ParentStudentNotificationTarget>(), null));

        public Task<IReadOnlyCollection<ParentNotificationTarget>> GetApprovedParentsForStudentAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(_targets);

        public Task<IReadOnlyCollection<ParentStudentNotificationTarget>> ListAllApprovedTargetsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ParentStudentNotificationTarget>>([]);
    }

    private sealed class SequentialIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
