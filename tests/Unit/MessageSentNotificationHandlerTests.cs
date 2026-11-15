using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P10 Task 3 / M16-1 + M09-2: Yeni mesajda alıcıya in-app bildirim.
/// <para>
/// Kritik davranış — <b>push kararı burada verilmez</b>: handler yalnız <c>UserNotification</c>
/// yazar, gönderim turunu <c>NotificationDispatcher</c> yürütür ve tercih kapısını orada uygular.
/// Böylece push'u kapatan kullanıcı bildirimi yine de bildirim merkezinde görür.
/// </para>
/// </summary>
public sealed class MessageSentNotificationHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ConversationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid MessageId = Guid.Parse("66666666-0000-0000-0000-000000000001");
    private static readonly Guid SenderUserId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid RecipientUserId = Guid.Parse("33333333-0000-0000-0000-000000000001");

    [Fact]
    public async Task New_Message_Should_Notify_The_Recipient()
    {
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository();
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, userNotifications);

        await handler.HandleAsync(Envelope(Guid.NewGuid()));

        var notification = Assert.Single(await userNotifications.ListPendingPushAsync(10, default));
        Assert.Equal(RecipientUserId, notification.UserId);
        Assert.Equal(UserNotificationKind.MessageReceived, notification.Kind);
        Assert.Equal($"/messages/{ConversationId}", notification.Route);
        Assert.Null(notification.PushedOnUtc);
    }

    /// <summary>Gönderen kendi mesajı için bildirim almaz — olay yalnız alıcıyı hedefler.</summary>
    [Fact]
    public async Task Sender_Is_Never_Notified()
    {
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository();
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, userNotifications);

        await handler.HandleAsync(Envelope(Guid.NewGuid()));

        Assert.DoesNotContain(
            await userNotifications.ListPendingPushAsync(10, default),
            notification => notification.UserId == SenderUserId);
    }

    [Fact]
    public async Task Same_Event_Twice_Should_Notify_Once()
    {
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository();
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, userNotifications);
        var envelope = Envelope(Guid.NewGuid());

        await handler.HandleAsync(envelope);
        await handler.HandleAsync(envelope);

        Assert.Single(await userNotifications.ListPendingPushAsync(10, default));
    }

    /// <summary>Bildirim gövdesi mesaj metni taşımaz.</summary>
    [Fact]
    public async Task Notification_Body_Never_Carries_The_Message_Preview()
    {
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository();
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, userNotifications);

        await handler.HandleAsync(Envelope(Guid.NewGuid(), preview: "Yarınki dersi 15:00'a alalım mı?"));

        var notification = Assert.Single(await userNotifications.ListPendingPushAsync(10, default));
        Assert.Equal("Bir kullanıcı size mesaj gönderdi.", notification.Body);
    }

    /// <summary>Başka modülün olayı bu handler'ı ilgilendirmez.</summary>
    [Fact]
    public void CanHandle_Only_Accepts_Messaging_MessageSent()
    {
        using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, new UserNotificationTests.FakeUserNotificationRepository());

        Assert.True(handler.CanHandle(Envelope(Guid.NewGuid())));
        Assert.False(handler.CanHandle(new IntegrationEvent(
            Guid.NewGuid(), Now, "MessageSentDomainEvent", "Notifications", "{}")));
        Assert.False(handler.CanHandle(new IntegrationEvent(
            Guid.NewGuid(), Now, "TopicGoalAchievedDomainEvent", "Messaging", "{}")));
    }

    [Fact]
    public async Task Malformed_Payload_Is_Not_Marked_Processed()
    {
        var userNotifications = new UserNotificationTests.FakeUserNotificationRepository();
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, userNotifications);

        await Assert.ThrowsAsync<PoisonMessageException>(() => handler.HandleAsync(new IntegrationEvent(
            Guid.NewGuid(), Now, "MessageSentDomainEvent", "Messaging", "null")));

        Assert.Empty(await userNotifications.ListPendingPushAsync(10, default));
        Assert.Empty(dbContext.InboxMessages);
    }

    private static MessageSentNotificationHandler NewHandler(
        NotificationsDbContext dbContext, IUserNotificationRepository userNotifications)
        => new(dbContext, userNotifications, new MessagingIdGenerator(), new MessagingClock(Now));

    private static NotificationsDbContext NewDbContext() =>
        new(
            new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new NoOpDomainEventMapper());

    private static IntegrationEvent Envelope(Guid eventId, string preview = "Merhaba") =>
        new(
            eventId,
            Now,
            "MessageSentDomainEvent",
            "Messaging",
            $$"""
            {"conversationId":"{{ConversationId}}","messageId":"{{MessageId}}","senderUserId":"{{SenderUserId}}","recipientUserId":"{{RecipientUserId}}","preview":"{{preview}}","sentOnUtc":"2026-09-04T12:00:00Z"}
            """);
}
