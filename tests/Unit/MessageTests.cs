using EgitimUssu.Modules.Messaging.Domain;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P10 Task 1 / M16-1: Tek mesajın domain kuralları — okundu işareti ve yumuşak silme.
/// <para>
/// Kritik davranış — <b>yumuşak silme içeriği veritabanından silmez</b>: şikayet/moderasyon
/// (M18) silinmiş bir mesajı incelemek zorunda kalabilir. Satır durur, API'ye <c>null</c> gider.
/// </para>
/// </summary>
public sealed class MessageTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ConversationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TeacherUserId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid StudentUserId = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid StudentId = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly Guid MessageId = Guid.Parse("66666666-0000-0000-0000-000000000001");

    [Fact]
    public void Sent_Message_Should_Start_Unread_And_Not_Deleted()
    {
        var message = NewMessage();

        Assert.Equal(ConversationId, message.ConversationId);
        Assert.Equal(TeacherUserId, message.SenderUserId);
        Assert.Equal("Merhaba", message.Body);
        Assert.Equal(Now, message.SentOnUtc);
        Assert.Null(message.ReadOnUtc);
        Assert.False(message.IsDeleted);
    }

    /// <summary>Gövde kenar boşluklarından arındırılır: "  Merhaba  " ile "Merhaba" aynı mesajdır.</summary>
    [Fact]
    public void Sent_Message_Should_Trim_Body()
    {
        var conversation = NewConversation();
        var message = conversation.SendMessage(MessageId, TeacherUserId, "  Merhaba  ", Now).Value!;

        Assert.Equal("Merhaba", message.Body);
    }

    [Fact]
    public void MarkRead_Should_Be_Idempotent()
    {
        var message = NewMessage();

        message.MarkRead(Now.AddMinutes(5));
        message.MarkRead(Now.AddMinutes(9));

        Assert.Equal(Now.AddMinutes(5), message.ReadOnUtc);
    }

    [Fact]
    public void SoftDelete_Should_Hide_Body_In_Response_But_Keep_Row()
    {
        var message = NewMessage();

        message.SoftDelete(Now.AddMinutes(1));

        Assert.True(message.IsDeleted);
        Assert.Equal(Now.AddMinutes(1), message.DeletedOnUtc);
        Assert.Equal("Merhaba", message.Body);
        Assert.Null(message.VisibleBody);
    }

    [Fact]
    public void VisibleBody_Should_Return_Body_While_Not_Deleted()
    {
        var message = NewMessage();

        Assert.Equal("Merhaba", message.VisibleBody);
    }

    [Fact]
    public void SoftDelete_Should_Be_Idempotent()
    {
        var message = NewMessage();

        message.SoftDelete(Now.AddMinutes(1));
        message.SoftDelete(Now.AddMinutes(3));

        Assert.Equal(Now.AddMinutes(1), message.DeletedOnUtc);
    }

    /// <summary>Silinmiş mesaj okundu sayılmaz: gelen kutusundaki okunmamış sayacı onu görmemelidir.</summary>
    [Fact]
    public void MarkRead_Should_Be_Ignored_For_Deleted_Message()
    {
        var message = NewMessage();
        message.SoftDelete(Now.AddMinutes(1));

        message.MarkRead(Now.AddMinutes(2));

        Assert.Null(message.ReadOnUtc);
    }

    private static Conversation NewConversation()
    {
        var conversation = Conversation.Create(
            ConversationId,
            ParticipantRole.Teacher,
            TeacherUserId,
            ParticipantRole.Student,
            StudentUserId,
            StudentId,
            Now).Value!;
        conversation.ClearDomainEvents();
        return conversation;
    }

    private static Message NewMessage()
        => NewConversation().SendMessage(MessageId, TeacherUserId, "Merhaba", Now).Value!;
}
