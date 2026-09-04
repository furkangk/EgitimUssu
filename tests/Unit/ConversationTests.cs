using EgitimUssu.Modules.Messaging.Domain;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P10 Task 1 / M16-1: Sohbetin saf domain kuralları.
/// <para>
/// Kritik davranış — <b>izinli çift kuralı burada, uygulama katmanında değil</b> zorlanır: PRD
/// yalnız öğretmen↔öğrenci ve öğretmen↔veli sohbetine izin verir. Kural domainde durmazsa
/// ileride eklenecek her yeni uç (Admin, içe aktarma, seed) yasak çifti sessizce açabilir.
/// </para>
/// </summary>
public sealed class ConversationTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ConversationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TeacherUserId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid StudentUserId = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid ParentUserId = Guid.Parse("44444444-0000-0000-0000-000000000001");
    private static readonly Guid OtherStudentUserId = Guid.Parse("33333333-0000-0000-0000-000000000002");
    private static readonly Guid StudentId = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly Guid MessageId = Guid.Parse("66666666-0000-0000-0000-000000000001");

    [Fact]
    public void Create_Should_Reject_Student_To_Student_Pair()
    {
        var result = Conversation.Create(
            ConversationId,
            ParticipantRole.Student,
            StudentUserId,
            ParticipantRole.Student,
            OtherStudentUserId,
            StudentId,
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.pair_not_allowed", result.Error.Code);
    }

    [Fact]
    public void Create_Should_Reject_Student_To_Parent_Pair()
    {
        var result = Conversation.Create(
            ConversationId,
            ParticipantRole.Student,
            StudentUserId,
            ParticipantRole.Parent,
            ParentUserId,
            StudentId,
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.pair_not_allowed", result.Error.Code);
    }

    [Fact]
    public void Create_Should_Reject_Teacher_To_Teacher_Pair()
    {
        var result = Conversation.Create(
            ConversationId,
            ParticipantRole.Teacher,
            TeacherUserId,
            ParticipantRole.Teacher,
            Guid.Parse("22222222-0000-0000-0000-000000000002"),
            StudentId,
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.pair_not_allowed", result.Error.Code);
    }

    [Fact]
    public void Create_Should_Reject_Conversation_With_Self()
    {
        var result = Conversation.Create(
            ConversationId,
            ParticipantRole.Teacher,
            TeacherUserId,
            ParticipantRole.Student,
            TeacherUserId,
            StudentId,
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.self_conversation", result.Error.Code);
    }

    [Fact]
    public void Create_Should_Allow_Teacher_Student()
    {
        var result = Conversation.Create(
            ConversationId,
            ParticipantRole.Teacher,
            TeacherUserId,
            ParticipantRole.Student,
            StudentUserId,
            StudentId,
            Now);

        Assert.True(result.IsSuccess);
        var conversation = result.Value!;
        Assert.Equal(TeacherUserId, conversation.TeacherUserId);
        Assert.Equal(StudentUserId, conversation.CounterpartUserId);
        Assert.Equal(ParticipantRole.Student, conversation.CounterpartRole);
        Assert.Equal(StudentId, conversation.StudentId);
        Assert.Equal(Now, conversation.CreatedOnUtc);
        Assert.Null(conversation.LastMessagePreview);
    }

    /// <summary>
    /// Öğrenci sohbeti başlatsa bile kayıt <b>normalize</b> edilir: öğretmen her zaman
    /// <c>TeacherUserId</c> tarafındadır. Aksi hâlde "aynı çift için tek sohbet" benzersiz
    /// indeksi (TeacherUserId, CounterpartUserId, StudentId) iki satıra izin verirdi.
    /// </summary>
    [Fact]
    public void Create_Should_Normalize_Teacher_To_Teacher_Side_When_Student_Initiates()
    {
        var result = Conversation.Create(
            ConversationId,
            ParticipantRole.Student,
            StudentUserId,
            ParticipantRole.Teacher,
            TeacherUserId,
            StudentId,
            Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(TeacherUserId, result.Value!.TeacherUserId);
        Assert.Equal(StudentUserId, result.Value!.CounterpartUserId);
        Assert.Equal(ParticipantRole.Student, result.Value!.CounterpartRole);
    }

    [Fact]
    public void Create_Should_Allow_Teacher_Parent_With_Student_Context()
    {
        var result = Conversation.Create(
            ConversationId,
            ParticipantRole.Teacher,
            TeacherUserId,
            ParticipantRole.Parent,
            ParentUserId,
            StudentId,
            Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParticipantRole.Parent, result.Value!.CounterpartRole);
        Assert.Equal(StudentId, result.Value!.StudentId);
    }

    [Fact]
    public void SendMessage_Should_Update_Preview_And_Raise_Event_To_Counterpart()
    {
        var conversation = NewTeacherStudentConversation();

        var result = conversation.SendMessage(MessageId, TeacherUserId, "Yarınki dersi 15:00'a alalım mı?", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("Yarınki dersi 15:00'a alalım mı?", conversation.LastMessagePreview);
        Assert.Equal(Now, conversation.LastMessageOnUtc);

        var raised = Assert.Single(conversation.DomainEvents);
        var sent = Assert.IsType<MessageSentDomainEvent>(raised);
        Assert.Equal(MessageId, sent.MessageId);
        Assert.Equal(TeacherUserId, sent.SenderUserId);
        Assert.Equal(StudentUserId, sent.RecipientUserId);
    }

    [Fact]
    public void SendMessage_From_Counterpart_Should_Target_Teacher_As_Recipient()
    {
        var conversation = NewTeacherStudentConversation();

        var result = conversation.SendMessage(MessageId, StudentUserId, "Olur hocam.", Now);

        Assert.True(result.IsSuccess);
        var sent = Assert.IsType<MessageSentDomainEvent>(Assert.Single(conversation.DomainEvents));
        Assert.Equal(TeacherUserId, sent.RecipientUserId);
    }

    [Fact]
    public void SendMessage_Should_Reject_Non_Participant()
    {
        var conversation = NewTeacherStudentConversation();

        var result = conversation.SendMessage(MessageId, ParentUserId, "Merhaba", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.not_participant", result.Error.Code);
        Assert.Empty(conversation.DomainEvents);
    }

    [Fact]
    public void SendMessage_Should_Reject_Empty_Body()
    {
        var conversation = NewTeacherStudentConversation();

        var result = conversation.SendMessage(MessageId, TeacherUserId, "   ", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.empty_body", result.Error.Code);
    }

    [Fact]
    public void SendMessage_Should_Reject_Body_Longer_Than_Limit()
    {
        var conversation = NewTeacherStudentConversation();

        var result = conversation.SendMessage(
            MessageId, TeacherUserId, new string('a', Message.MaxBodyLength + 1), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.body_too_long", result.Error.Code);
    }

    /// <summary>Önizleme gelen kutusunda tek satırdır: uzun mesaj kısaltılır, satır sonu boşluğa döner.</summary>
    [Fact]
    public void SendMessage_Should_Truncate_Preview()
    {
        var conversation = NewTeacherStudentConversation();

        conversation.SendMessage(MessageId, TeacherUserId, new string('a', 400), Now);

        Assert.NotNull(conversation.LastMessagePreview);
        Assert.Equal(Conversation.PreviewMaxLength, conversation.LastMessagePreview!.Length);
    }

    [Fact]
    public void Blocked_By_Counterpart_Should_Prevent_Teacher_Send()
    {
        var conversation = NewTeacherStudentConversation();

        var blocked = conversation.Block(StudentUserId, Now);

        Assert.True(blocked.IsSuccess);
        Assert.True(conversation.IsBlockedByCounterpart);
        Assert.False(conversation.CanSend(TeacherUserId));
        Assert.False(conversation.CanSend(StudentUserId));

        var send = conversation.SendMessage(MessageId, TeacherUserId, "Merhaba", Now);
        Assert.True(send.IsFailure);
        Assert.Equal("messaging.blocked", send.Error.Code);
    }

    [Fact]
    public void Unblock_Should_Restore_Sending()
    {
        var conversation = NewTeacherStudentConversation();
        conversation.Block(StudentUserId, Now);

        var unblocked = conversation.Unblock(StudentUserId, Now.AddMinutes(1));

        Assert.True(unblocked.IsSuccess);
        Assert.False(conversation.IsBlockedByCounterpart);
        Assert.True(conversation.CanSend(TeacherUserId));
        Assert.True(conversation.SendMessage(MessageId, TeacherUserId, "Merhaba", Now.AddMinutes(2)).IsSuccess);
    }

    /// <summary>
    /// Engeli yalnız koyan taraf kaldırabilir: öğretmen, öğrencinin koyduğu engeli kaldırabilseydi
    /// engelleme özelliği hiçbir şey ifade etmezdi.
    /// </summary>
    [Fact]
    public void Unblock_Should_Not_Lift_Block_Placed_By_The_Other_Side()
    {
        var conversation = NewTeacherStudentConversation();
        conversation.Block(StudentUserId, Now);

        conversation.Unblock(TeacherUserId, Now.AddMinutes(1));

        Assert.True(conversation.IsBlockedByCounterpart);
        Assert.False(conversation.CanSend(TeacherUserId));
    }

    [Fact]
    public void Block_Should_Reject_Non_Participant()
    {
        var conversation = NewTeacherStudentConversation();

        var result = conversation.Block(ParentUserId, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.not_participant", result.Error.Code);
    }

    [Fact]
    public void HasParticipant_Should_Recognize_Both_Sides_Only()
    {
        var conversation = NewTeacherStudentConversation();

        Assert.True(conversation.HasParticipant(TeacherUserId));
        Assert.True(conversation.HasParticipant(StudentUserId));
        Assert.False(conversation.HasParticipant(ParentUserId));
    }

    [Fact]
    public void RecipientOf_Should_Return_The_Other_Side()
    {
        var conversation = NewTeacherStudentConversation();

        Assert.Equal(StudentUserId, conversation.RecipientOf(TeacherUserId));
        Assert.Equal(TeacherUserId, conversation.RecipientOf(StudentUserId));
    }

    private static Conversation NewTeacherStudentConversation()
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
}
