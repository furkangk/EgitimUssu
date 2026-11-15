using EgitimUssu.Modules.Messaging.Application;
using EgitimUssu.Modules.Messaging.Domain;
namespace EgitimUssu.Tests.Unit;
public sealed class MessageReadMarkerTests
{
    private static readonly DateTime Now=new(2026,11,15,12,0,0,DateTimeKind.Utc);
    [Fact]
    public async Task Read_Marker_Excludes_Newer_Unseen_And_Same_Instant_Other_Messages()
    {
        var (repo,reader,conversation)=Seed();
        var older=Add(repo,conversation,Now.AddMinutes(-1)); var anchor=Add(repo,conversation,Now);
        var simultaneous=Add(repo,conversation,Now); var newer=Add(repo,conversation,Now.AddSeconds(1));
        var handler=new MarkConversationReadCommandHandler(new FakeMessagingCurrentUser(reader,"Student"),repo,new MessagingClock(Now.AddMinutes(1)));
        Assert.True((await handler.Handle(new(conversation.Id,anchor.Id),default)).IsSuccess);
        Assert.NotNull(older.ReadOnUtc);Assert.NotNull(anchor.ReadOnUtc);
        Assert.Null(simultaneous.ReadOnUtc);Assert.Null(newer.ReadOnUtc);
        await handler.Handle(new(conversation.Id,anchor.Id),default);
        Assert.Equal(Now.AddMinutes(1),anchor.ReadOnUtc);
    }
    [Fact]
    public async Task Missing_Marker_Is_Safe_NoOp_And_Foreign_Marker_Is_Rejected()
    {
        var (repo,reader,conversation)=Seed(); var unseen=Add(repo,conversation,Now);
        var handler=new MarkConversationReadCommandHandler(new FakeMessagingCurrentUser(reader,"Student"),repo,new MessagingClock(Now));
        Assert.True((await handler.Handle(new(conversation.Id),default)).IsSuccess);Assert.Null(unseen.ReadOnUtc);
        var other=Conversation.Create(Guid.NewGuid(),ParticipantRole.Teacher,Guid.NewGuid(),ParticipantRole.Student,reader,Guid.NewGuid(),Now).Value!;
        var foreign=Add(repo,other,Now);
        Assert.True((await handler.Handle(new(conversation.Id,foreign.Id),default)).IsFailure);Assert.Null(unseen.ReadOnUtc);
    }
    private static (FakeMessagingRepository,Guid,Conversation) Seed()
    {
        var reader=Guid.NewGuid(); var conversation=Conversation.Create(Guid.NewGuid(),ParticipantRole.Teacher,Guid.NewGuid(),ParticipantRole.Student,reader,Guid.NewGuid(),Now).Value!;
        var repo=new FakeMessagingRepository();repo.Conversations.Add(conversation);return(repo,reader,conversation);
    }
    private static Message Add(FakeMessagingRepository repo,Conversation conversation,DateTime time)
    {var message=conversation.SendMessage(Guid.NewGuid(),conversation.TeacherUserId,"metin",time).Value!;repo.Messages.Add(message);return message;}
}
