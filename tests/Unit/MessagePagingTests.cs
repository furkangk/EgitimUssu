using EgitimUssu.Modules.Messaging.Application;
using EgitimUssu.Modules.Messaging.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P10 Task 2 / M16-1: Keyset sayfalama ve gelen kutusu sayaçları.
/// <para>
/// Kritik davranış — <b>offset değil keyset</b>: sohbet listelenirken yeni mesaj gelirse offset
/// tabanlı sayfalama satırları kaydırır, kullanıcı bir mesajı iki kez görür ya da hiç görmez.
/// İmleç son görülen mesajın zamanıdır (<c>beforeUtc</c>), sıralama yeniden eskiye.
/// </para>
/// </summary>
public sealed class MessagePagingTests
{
    private static readonly DateTime Start = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherUserId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid StudentUserId = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid StudentId = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly Guid ConversationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    [Fact]
    public async Task Messages_Are_Returned_Newest_First()
    {
        var repository = Seed(5);

        var page = await ListAsync(repository, TeacherUserId, beforeUtc: null, take: 5);

        Assert.Equal(5, page.Count);
        Assert.Equal("mesaj-5", page[0].Body);
        Assert.Equal("mesaj-1", page[4].Body);
    }

    [Fact]
    public async Task Keyset_Paging_Produces_No_Duplicates_And_No_Gaps()
    {
        var repository = Seed(10);

        var first = await ListAsync(repository, TeacherUserId, beforeUtc: null, take: 4);
        var second = await ListAsync(repository, TeacherUserId, beforeUtc: first[^1].SentOnUtc, take: 4);
        var third = await ListAsync(repository, TeacherUserId, beforeUtc: second[^1].SentOnUtc, take: 4);

        var seen = first.Concat(second).Concat(third).Select(row => row.Body).ToList();

        Assert.Equal(10, seen.Count);
        Assert.Equal(10, seen.Distinct().Count());
        Assert.Equal(
            Enumerable.Range(1, 10).Reverse().Select(index => $"mesaj-{index}"),
            seen);
    }

    /// <summary>
    /// Sayfa çekilirken yeni mesaj gelirse ikinci sayfa kaymaz: imleç zamandır, ofset değil.
    /// Offset tabanlı bir sayfalama burada 4. mesajı ikinci kez döndürürdü.
    /// </summary>
    [Fact]
    public async Task New_Message_Arriving_Mid_Paging_Does_Not_Shift_The_Next_Page()
    {
        var repository = Seed(8);

        var first = await ListAsync(repository, TeacherUserId, beforeUtc: null, take: 4);
        Append(repository, 9);
        var second = await ListAsync(repository, TeacherUserId, beforeUtc: first[^1].SentOnUtc, take: 4);

        Assert.Equal(["mesaj-4", "mesaj-3", "mesaj-2", "mesaj-1"], second.Select(row => row.Body));
    }

    [Fact]
    public async Task Inbox_Reports_Unread_Count_And_Last_Preview_For_The_Reader()
    {
        var repository = Seed(3, senderUserId: StudentUserId);

        var inbox = await InboxAsync(repository, TeacherUserId);

        var row = Assert.Single(inbox);
        Assert.Equal(3, row.UnreadCount);
        Assert.Equal("mesaj-3", row.LastMessagePreview);
    }

    /// <summary>Kendi gönderdiğin mesaj sana okunmamış sayılmaz.</summary>
    [Fact]
    public async Task Own_Messages_Do_Not_Count_As_Unread()
    {
        var repository = Seed(3, senderUserId: TeacherUserId);

        var inbox = await InboxAsync(repository, TeacherUserId);

        Assert.Equal(0, Assert.Single(inbox).UnreadCount);
    }

    [Fact]
    public async Task Mark_Read_Clears_The_Unread_Counter_For_The_Reader_Only()
    {
        var repository = Seed(3, senderUserId: StudentUserId);

        var marked = await new MarkConversationReadCommandHandler(
                new FakeMessagingCurrentUser(TeacherUserId, "Teacher"), repository, new MessagingClock(Start.AddHours(1)))
            .Handle(new MarkConversationReadCommand(ConversationId), CancellationToken.None);

        Assert.True(marked.IsSuccess);
        Assert.Equal(0, Assert.Single(await InboxAsync(repository, TeacherUserId)).UnreadCount);
        Assert.All(repository.Messages, message => Assert.Equal(Start.AddHours(1), message.ReadOnUtc));
    }

    [Fact]
    public async Task Mark_Read_Is_Idempotent()
    {
        var repository = Seed(2, senderUserId: StudentUserId);
        var handler = new MarkConversationReadCommandHandler(
            new FakeMessagingCurrentUser(TeacherUserId, "Teacher"), repository, new MessagingClock(Start.AddHours(1)));

        await handler.Handle(new MarkConversationReadCommand(ConversationId), CancellationToken.None);
        await handler.Handle(new MarkConversationReadCommand(ConversationId), CancellationToken.None);

        Assert.All(repository.Messages, message => Assert.Equal(Start.AddHours(1), message.ReadOnUtc));
    }

    [Fact]
    public async Task Take_Is_Clamped_To_The_Page_Limit()
    {
        var repository = Seed(MessagingPaging.MaxTake + 20);

        var page = await ListAsync(repository, TeacherUserId, beforeUtc: null, take: MessagingPaging.MaxTake + 20);

        Assert.Equal(MessagingPaging.MaxTake, page.Count);
    }

    [Fact]
    public async Task Non_Positive_Take_Falls_Back_To_The_Default_Page_Size()
    {
        var repository = Seed(MessagingPaging.DefaultTake + 5);

        var page = await ListAsync(repository, TeacherUserId, beforeUtc: null, take: 0);

        Assert.Equal(MessagingPaging.DefaultTake, page.Count);
    }

    private static async Task<IReadOnlyList<MessageResponse>> ListAsync(
        FakeMessagingRepository repository, Guid userId, DateTime? beforeUtc, int take)
    {
        var result = await new ListMessagesQueryHandler(
                new FakeMessagingCurrentUser(userId, "Teacher"), repository)
            .Handle(new ListMessagesQuery(ConversationId, beforeUtc, take), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static async Task<IReadOnlyList<ConversationSummaryResponse>> InboxAsync(
        FakeMessagingRepository repository, Guid userId)
    {
        // Sayfalama ilişkiden bağımsızdır; bağsız dizinle her satır salt-okunur gelir, bu testin konusu değil.
        var students = new FakeMessagingStudentDirectory();
        var policy = new ConversationRelationshipPolicy(students, students, new FakeMessagingParentAccessDirectory());
        var result = await new ListConversationsQueryHandler(
                new FakeMessagingCurrentUser(userId, "Teacher"), repository, policy)
            .Handle(new ListConversationsQuery(0, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static FakeMessagingRepository Seed(int messageCount, Guid? senderUserId = null)
    {
        var repository = new FakeMessagingRepository();
        var conversation = Conversation.Create(
            ConversationId,
            ParticipantRole.Teacher,
            TeacherUserId,
            ParticipantRole.Student,
            StudentUserId,
            StudentId,
            Start).Value!;
        repository.Conversations.Add(conversation);

        for (var index = 1; index <= messageCount; index++)
        {
            Append(repository, index, senderUserId);
        }

        return repository;
    }

    private static void Append(FakeMessagingRepository repository, int index, Guid? senderUserId = null)
    {
        var conversation = repository.Conversations.Single();
        var message = conversation.SendMessage(
            Guid.Parse($"66666666-0000-0000-0000-{index:D12}"),
            senderUserId ?? TeacherUserId,
            $"mesaj-{index}",
            Start.AddMinutes(index)).Value!;
        conversation.ClearDomainEvents();
        repository.Messages.Add(message);
    }
}
