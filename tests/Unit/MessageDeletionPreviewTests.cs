using EgitimUssu.Modules.Messaging.Application;
using EgitimUssu.Modules.Messaging.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class MessageDeletionPreviewTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deleting_Last_Message_Hides_Preview_From_Both_Inboxes(bool asAdmin)
    {
        var (repository, conversation) = CreateConversation();
        var message = conversation.SendMessage(Guid.NewGuid(), conversation.TeacherUserId,
            "Silinecek\r\n" + new string('x', 160), Now).Value!;
        repository.Messages.Add(message);
        var caller = asAdmin
            ? new FakeMessagingCurrentUser(Guid.NewGuid(), "Admin")
            : new FakeMessagingCurrentUser(conversation.TeacherUserId, "Teacher");

        var result = await new DeleteMessageCommandHandler(caller, repository, new MessagingClock(Now.AddMinutes(1)))
            .Handle(new DeleteMessageCommand(message.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(message.VisibleBody);
        Assert.Null(conversation.LastMessagePreview);
        Assert.Equal(Now, conversation.LastMessageOnUtc);
        foreach (var userId in new[] { conversation.TeacherUserId, conversation.CounterpartUserId })
        {
            var inbox = await repository.ListInboxAsync(userId, 0, 10, CancellationToken.None);
            Assert.Null(MessagingResponseFactory.From(Assert.Single(inbox), userId).LastMessagePreview);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deleting_Older_Message_Preserves_Different_Last_Preview(bool sameTimestamp)
    {
        var (repository, conversation) = CreateConversation();
        var older = conversation.SendMessage(Guid.NewGuid(), conversation.TeacherUserId, "Eski mesaj", Now).Value!;
        var latest = conversation.SendMessage(Guid.NewGuid(), conversation.CounterpartUserId, "Yeni mesaj",
            sameTimestamp ? Now : Now.AddMinutes(1)).Value!;
        repository.Messages.AddRange([older, latest]);

        var result = await new DeleteMessageCommandHandler(
            new FakeMessagingCurrentUser(conversation.TeacherUserId, "Teacher"), repository, new MessagingClock(Now.AddMinutes(2)))
            .Handle(new DeleteMessageCommand(older.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(older.VisibleBody);
        Assert.Equal("Yeni mesaj", conversation.LastMessagePreview);
        Assert.Equal(latest.SentOnUtc, conversation.LastMessageOnUtc);
    }

    [Fact]
    public async Task Recipient_Cannot_Delete_Message_Or_Hide_Preview()
    {
        var (repository, conversation) = CreateConversation();
        var message = conversation.SendMessage(Guid.NewGuid(), conversation.TeacherUserId, "Merhaba", Now).Value!;
        repository.Messages.Add(message);

        var result = await new DeleteMessageCommandHandler(
            new FakeMessagingCurrentUser(conversation.CounterpartUserId, "Student"), repository, new MessagingClock(Now))
            .Handle(new DeleteMessageCommand(message.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(message.IsDeleted);
        Assert.Equal("Merhaba", conversation.LastMessagePreview);
        Assert.Equal(0, repository.SaveCount);
    }

    private static (FakeMessagingRepository Repository, Conversation Conversation) CreateConversation()
    {
        var repository = new FakeMessagingRepository();
        var conversation = Conversation.Create(Guid.NewGuid(), ParticipantRole.Teacher, Guid.NewGuid(),
            ParticipantRole.Student, Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-1)).Value!;
        repository.Conversations.Add(conversation);
        return (repository, conversation);
    }
}
