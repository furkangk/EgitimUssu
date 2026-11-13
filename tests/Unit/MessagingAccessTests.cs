using EgitimUssu.Modules.Messaging.Application;
using EgitimUssu.Modules.Messaging.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P10 Task 2 / M16-1: Sohbet açma ve mesaj gönderme yetkisi.
/// <para>
/// Kritik davranış — <b>ilişki zorunluluğu</b>: bir öğretmen, bağı olmayan bir öğrenciye mesaj
/// atamaz. Bu kapı olmazsa platform, herkesin herkese yazabildiği açık bir mesaj kutusuna dönerdi.
/// İlişki bilgisi Messaging'in kendi tablolarında değil, <c>Shared/Contracts</c> dizinlerinde durur.
/// </para>
/// </summary>
public sealed class MessagingAccessTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherUserId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid StudentUserId = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid ParentUserId = Guid.Parse("44444444-0000-0000-0000-000000000001");
    private static readonly Guid StrangerUserId = Guid.Parse("99999999-0000-0000-0000-000000000001");
    private static readonly Guid StudentId = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly Guid NewConversationId = Guid.Parse("11111111-0000-0000-0000-0000000000aa");
    private static readonly Guid NewMessageId = Guid.Parse("66666666-0000-0000-0000-0000000000aa");

    [Fact]
    public async Task Teacher_Without_Link_Cannot_Open_Conversation()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");

        var result = await fixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.relationship_required", result.Error.Code);
        Assert.Empty(fixture.Repository.Conversations);
    }

    [Fact]
    public async Task Linked_Teacher_Can_Open_Conversation_With_Student()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();

        var result = await fixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(NewConversationId, result.Value!.Id);
        Assert.Equal(TeacherUserId, result.Value!.TeacherUserId);
        Assert.Equal(nameof(ParticipantRole.Student), result.Value!.CounterpartRole);
        Assert.Single(fixture.Repository.Conversations);
    }

    /// <summary>
    /// Öğretmenin bağı olsa bile karşı taraf o öğrencinin kullanıcısı değilse sohbet açılmaz:
    /// yoksa doğru <c>studentId</c> ile yanlış bir <c>counterpartUserId</c> eşleştirilebilirdi.
    /// </summary>
    [Fact]
    public async Task Counterpart_Must_Be_The_Student_Behind_The_Student_Id()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();

        var result = await fixture.OpenAsync(StrangerUserId, nameof(ParticipantRole.Student), StudentId);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.relationship_required", result.Error.Code);
    }

    /// <summary>
    /// Öğretmen ekranı öğrencinin <b>kullanıcı</b> kimliğini bilmez (öğrenci listesi yalnız profil
    /// kimliği taşır); karşı tarafı sunucu <c>studentId</c>'den çözer.
    /// </summary>
    [Fact]
    public async Task Teacher_May_Omit_Counterpart_User_Id_For_Student()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();

        var result = await fixture.OpenAsync(Guid.Empty, nameof(ParticipantRole.Student), StudentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(StudentUserId, result.Value!.CounterpartUserId);
    }

    /// <summary>Öğrenci hesabını henüz devralmadıysa çözülecek bir kullanıcı yoktur.</summary>
    [Fact]
    public async Task Omitted_Counterpart_Fails_When_Student_Has_No_User_Account()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.Students.Links.Add((TeacherUserId, StudentId));
        fixture.Students.Owners[StudentId] = null;

        var result = await fixture.OpenAsync(Guid.Empty, nameof(ParticipantRole.Student), StudentId);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.relationship_required", result.Error.Code);
    }

    [Fact]
    public async Task Teacher_Can_Open_Conversation_With_Approved_Parent()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();
        fixture.ApproveParent();

        var result = await fixture.OpenAsync(ParentUserId, nameof(ParticipantRole.Parent), StudentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(ParticipantRole.Parent), result.Value!.CounterpartRole);
    }

    [Fact]
    public async Task Unapproved_Parent_Cannot_Open_Conversation()
    {
        var fixture = new Fixture(ParentUserId, "Parent");
        fixture.LinkTeacherToStudent();

        var result = await fixture.OpenAsync(TeacherUserId, nameof(ParticipantRole.Teacher), StudentId);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.relationship_required", result.Error.Code);
    }

    [Fact]
    public async Task Student_To_Parent_Pair_Is_Rejected_By_Domain()
    {
        var fixture = new Fixture(StudentUserId, "Student");
        fixture.LinkTeacherToStudent();
        fixture.ApproveParent();

        var result = await fixture.OpenAsync(ParentUserId, nameof(ParticipantRole.Parent), StudentId);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.pair_not_allowed", result.Error.Code);
    }

    /// <summary>Aynı çift + aynı öğrenci için ikinci çağrı yeni satır açmaz (get-or-create).</summary>
    [Fact]
    public async Task Opening_Twice_Returns_The_Same_Conversation()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();

        var first = await fixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);
        var second = await fixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Single(fixture.Repository.Conversations);
    }

    /// <summary>Öğrenci sohbeti başlatsa da aynı satıra düşer — kayıt normalize edilir.</summary>
    [Fact]
    public async Task Student_Opening_Finds_The_Conversation_Teacher_Started()
    {
        var teacherFixture = new Fixture(TeacherUserId, "Teacher");
        teacherFixture.LinkTeacherToStudent();
        var opened = await teacherFixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);

        var studentFixture = teacherFixture.As(StudentUserId, "Student");
        var result = await studentFixture.OpenAsync(TeacherUserId, nameof(ParticipantRole.Teacher), StudentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(opened.Value!.Id, result.Value!.Id);
        Assert.Single(teacherFixture.Repository.Conversations);
    }

    [Fact]
    public async Task Non_Participant_Cannot_List_Messages()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();
        var conversation = await fixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);

        var stranger = fixture.As(StrangerUserId, "Teacher");
        var result = await stranger.ListMessagesAsync(conversation.Value!.Id, null, 20);

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.not_participant", result.Error.Code);
    }

    [Fact]
    public async Task Non_Participant_Cannot_Send_Message()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();
        var conversation = await fixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);

        var stranger = fixture.As(StrangerUserId, "Teacher");
        var result = await stranger.SendAsync(conversation.Value!.Id, "Merhaba");

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.not_participant", result.Error.Code);
    }

    [Fact]
    public async Task Sending_To_Blocked_Conversation_Fails()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();
        var conversation = await fixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);

        var student = fixture.As(StudentUserId, "Student");
        Assert.True((await student.BlockAsync(conversation.Value!.Id, blocked: true)).IsSuccess);

        var result = await fixture.SendAsync(conversation.Value!.Id, "Merhaba");

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.blocked", result.Error.Code);
    }

    [Fact]
    public async Task Unblock_By_The_Blocking_Side_Reopens_Sending()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();
        var conversation = await fixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);

        var student = fixture.As(StudentUserId, "Student");
        await student.BlockAsync(conversation.Value!.Id, blocked: true);
        await student.BlockAsync(conversation.Value!.Id, blocked: false);

        var result = await fixture.SendAsync(conversation.Value!.Id, "Merhaba");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Only_Sender_Or_Admin_Can_Soft_Delete_A_Message()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();
        var conversation = await fixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);
        var message = await fixture.SendAsync(conversation.Value!.Id, "Merhaba");

        var student = fixture.As(StudentUserId, "Student");
        var forbidden = await student.DeleteAsync(message.Value!.Id);
        Assert.True(forbidden.IsFailure);
        Assert.Equal("messaging.not_participant", forbidden.Error.Code);

        var deleted = await fixture.DeleteAsync(message.Value!.Id);
        Assert.True(deleted.IsSuccess);
        Assert.True(fixture.Repository.Messages.Single().IsDeleted);
        Assert.Equal("Merhaba", fixture.Repository.Messages.Single().Body);
    }

    [Fact]
    public async Task Admin_Can_Soft_Delete_Any_Message()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();
        var conversation = await fixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);
        var message = await fixture.SendAsync(conversation.Value!.Id, "Merhaba");

        var admin = fixture.As(StrangerUserId, "Admin");
        var result = await admin.DeleteAsync(message.Value!.Id);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Deleted_Message_Body_Is_Hidden_In_Listing()
    {
        var fixture = new Fixture(TeacherUserId, "Teacher");
        fixture.LinkTeacherToStudent();
        var conversation = await fixture.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);
        var message = await fixture.SendAsync(conversation.Value!.Id, "Merhaba");
        await fixture.DeleteAsync(message.Value!.Id);

        var listed = await fixture.ListMessagesAsync(conversation.Value!.Id, null, 20);

        var row = Assert.Single(listed.Value!);
        Assert.True(row.IsDeleted);
        Assert.Null(row.Body);
    }

    /// <summary>
    /// Ortak kurulum. Aynı depo/dizinleri paylaşan farklı kullanıcı kimliğiyle yeni bir görünüm
    /// üretebilmek için <see cref="As"/> vardır: sohbetin iki tarafını tek testte oynatabilmek gerekir.
    /// </summary>
    private sealed class Fixture
    {
        private readonly Guid _userId;
        private readonly string[] _roles;

        public Fixture(Guid userId, params string[] roles)
        {
            _userId = userId;
            _roles = roles;
            Repository = new FakeMessagingRepository();
            Students = new FakeMessagingStudentDirectory();
            Parents = new FakeMessagingParentAccessDirectory();
        }

        private Fixture(Fixture source, Guid userId, string[] roles)
        {
            _userId = userId;
            _roles = roles;
            Repository = source.Repository;
            Students = source.Students;
            Parents = source.Parents;
        }

        public FakeMessagingRepository Repository { get; }

        public FakeMessagingStudentDirectory Students { get; }

        public FakeMessagingParentAccessDirectory Parents { get; }

        public Fixture As(Guid userId, params string[] roles) => new(this, userId, roles);

        public void LinkTeacherToStudent()
        {
            Students.Links.Add((TeacherUserId, StudentId));
            Students.Owners[StudentId] = StudentUserId;
        }

        public void ApproveParent() => Parents.Approved.Add((ParentUserId, StudentId));

        public Task<Result<ConversationResponse>> OpenAsync(Guid counterpartUserId, string role, Guid studentId)
            => new OpenConversationCommandHandler(
                    CurrentUser(), Repository, Policy(), new MessagingClock(Now), new MessagingIdGenerator(NewConversationId))
                .Handle(new OpenConversationCommand(counterpartUserId, role, studentId), CancellationToken.None);

        public Task<Result<MessageResponse>> SendAsync(Guid conversationId, string body)
            => new SendMessageCommandHandler(
                    CurrentUser(), Repository, new MessagingClock(Now), new MessagingIdGenerator(NewMessageId))
                .Handle(new SendMessageCommand(conversationId, body), CancellationToken.None);

        public Task<Result<IReadOnlyList<MessageResponse>>> ListMessagesAsync(
            Guid conversationId, DateTime? beforeUtc, int take)
            => new ListMessagesQueryHandler(CurrentUser(), Repository)
                .Handle(new ListMessagesQuery(conversationId, beforeUtc, take), CancellationToken.None);

        public Task<Result<ConversationResponse>> BlockAsync(Guid conversationId, bool blocked)
            => new SetConversationBlockCommandHandler(CurrentUser(), Repository, new MessagingClock(Now))
                .Handle(new SetConversationBlockCommand(conversationId, blocked), CancellationToken.None);

        public Task<Result> DeleteAsync(Guid messageId)
            => new DeleteMessageCommandHandler(CurrentUser(), Repository, new MessagingClock(Now))
                .Handle(new DeleteMessageCommand(messageId), CancellationToken.None);

        private FakeMessagingCurrentUser CurrentUser() => new(_userId, _roles);

        private ConversationRelationshipPolicy Policy() => new(Students, Parents);
    }
}
