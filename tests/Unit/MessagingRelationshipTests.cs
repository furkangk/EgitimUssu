using EgitimUssu.Modules.Messaging.Application;
using EgitimUssu.Modules.Messaging.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P15 Task 5 / N-46, K-28: Mesajlaşmada <b>süregelen</b> ilişki kontrolü.
/// <para>
/// Eskiden ilişki yalnız sohbet <b>açılırken</b> soruluyordu; bağ arşivlenince ya da veli bağı iptal edilince
/// taraflar aynı sohbete yazmayı sürdürüyordu. Kural (K-28): ilişki bitince <b>gönderim kapanır</b>, geçmiş
/// salt-okunur kalır — kullanıcının kendi geçmişini görmesi meşru, yeni temas değil. İlişki yeniden kurulursa
/// gönderim geri açılır (bilgi saklanmaz, her seferinde kaynaktan sorulur — #191).
/// </para>
/// </summary>
public sealed class MessagingRelationshipTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherUserId = Guid.Parse("22222222-0000-0000-0000-0000000000b1");
    private static readonly Guid StudentUserId = Guid.Parse("33333333-0000-0000-0000-0000000000b1");
    private static readonly Guid OtherStudentUserId = Guid.Parse("33333333-0000-0000-0000-0000000000b2");
    private static readonly Guid ParentUserId = Guid.Parse("44444444-0000-0000-0000-0000000000b1");
    private static readonly Guid StrangerUserId = Guid.Parse("99999999-0000-0000-0000-0000000000b1");
    private static readonly Guid StudentId = Guid.Parse("55555555-0000-0000-0000-0000000000b1");
    private static readonly Guid OtherStudentId = Guid.Parse("55555555-0000-0000-0000-0000000000b2");

    /// <summary>Pozitif kontrol (#196): ilişki sürüyorsa davranış değişmez — gönderim açık, sohbet yazılabilir.</summary>
    [Fact]
    public async Task Ongoing_Relationship_Keeps_Sending_Open()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);

        var opened = await teacher.OpenStudentAsync(StudentId);
        var sent = await teacher.SendAsync(opened.Id, "Merhaba");
        var reply = await teacher.As(StudentUserId, "Student").SendAsync(opened.Id, "Merhaba hocam");

        Assert.False(opened.IsReadOnly);
        Assert.True(sent.IsSuccess);
        Assert.True(reply.IsSuccess);
        Assert.False(Assert.Single(await teacher.InboxAsync()).IsReadOnly);
    }

    [Fact]
    public async Task Teacher_Cannot_Send_After_The_Link_Is_Archived()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        var opened = await teacher.OpenStudentAsync(StudentId);
        Assert.True((await teacher.SendAsync(opened.Id, "Merhaba")).IsSuccess);

        teacher.EndTeacherLink(StudentId);
        var result = await teacher.SendAsync(opened.Id, "Hâlâ orada mısın?");

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.relationship_ended", result.Error.Code);
        Assert.Single(teacher.Repository.Messages);
    }

    /// <summary>İlişki iki tarafı birden bağlar: öğretmen bağı bitince öğrenci de yazamaz.</summary>
    [Fact]
    public async Task Student_Cannot_Send_After_The_Teacher_Link_Is_Archived()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        var opened = await teacher.OpenStudentAsync(StudentId);

        teacher.EndTeacherLink(StudentId);
        var result = await teacher.As(StudentUserId, "Student").SendAsync(opened.Id, "Hocam?");

        Assert.True(result.IsFailure);
        Assert.Equal("messaging.relationship_ended", result.Error.Code);
    }

    [Fact]
    public async Task Parent_And_Teacher_Cannot_Send_After_The_Parent_Link_Is_Revoked()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        teacher.ApproveParent(StudentId);
        var opened = await teacher.OpenParentAsync(StudentId);
        var parent = teacher.As(ParentUserId, "Parent");
        Assert.True((await parent.SendAsync(opened.Id, "Merhaba")).IsSuccess);

        teacher.RevokeParent(StudentId);
        var fromParent = await parent.SendAsync(opened.Id, "Bir sorum var");
        var fromTeacher = await teacher.SendAsync(opened.Id, "Buyurun");

        Assert.Equal("messaging.relationship_ended", fromParent.Error.Code);
        Assert.Equal("messaging.relationship_ended", fromTeacher.Error.Code);
    }

    /// <summary>Veli sohbeti öğretmenin o öğrenciyle yaşayan bağına da dayanır: bağ arşivlenince veli de yazamaz.</summary>
    [Fact]
    public async Task Ending_The_Teacher_Link_Also_Closes_The_Parent_Conversation()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        teacher.ApproveParent(StudentId);
        var opened = await teacher.OpenParentAsync(StudentId);

        teacher.EndTeacherLink(StudentId);
        var result = await teacher.As(ParentUserId, "Parent").SendAsync(opened.Id, "Merhaba");

        Assert.Equal("messaging.relationship_ended", result.Error.Code);
    }

    /// <summary>K-28: geçmiş görünür kalır; var olan sohbeti yeniden açmak 409 değil, salt-okunur sohbettir.</summary>
    [Fact]
    public async Task History_Stays_Readable_And_The_Conversation_Reopens_Read_Only()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        var opened = await teacher.OpenStudentAsync(StudentId);
        await teacher.SendAsync(opened.Id, "Merhaba");

        teacher.EndTeacherLink(StudentId);
        var history = await teacher.As(StudentUserId, "Student").ListMessagesAsync(opened.Id);
        var reopened = await teacher.OpenAsync(StudentUserId, nameof(ParticipantRole.Student), StudentId);

        Assert.True(history.IsSuccess);
        Assert.Single(history.Value!);
        Assert.True(reopened.IsSuccess);
        Assert.Equal(opened.Id, reopened.Value!.Id);
        Assert.True(reopened.Value!.IsReadOnly);
    }

    /// <summary>Gelen kutusu satır satır işaretler: biten ilişki salt-okunur, süren ilişki değil.</summary>
    [Fact]
    public async Task Inbox_Flags_Only_Ended_Conversations_As_Read_Only()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        teacher.Link(OtherStudentId, OtherStudentUserId);
        var ended = await teacher.OpenStudentAsync(StudentId);
        var ongoing = await teacher.OpenStudentAsync(OtherStudentId);

        teacher.EndTeacherLink(StudentId);
        var inbox = await teacher.InboxAsync();

        Assert.True(inbox.Single(row => row.Id == ended.Id).IsReadOnly);
        Assert.False(inbox.Single(row => row.Id == ongoing.Id).IsReadOnly);
        Assert.True(Assert.Single(await teacher.As(StudentUserId, "Student").InboxAsync()).IsReadOnly);
    }

    /// <summary>Bilgi saklanmaz, kaynaktan sorulur: bağ arşivden çıkınca gönderim kendiliğinden geri açılır.</summary>
    [Fact]
    public async Task Relinking_Reopens_Sending()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        var opened = await teacher.OpenStudentAsync(StudentId);
        teacher.EndTeacherLink(StudentId);
        Assert.True((await teacher.SendAsync(opened.Id, "Merhaba")).IsFailure);

        teacher.Link(StudentId, StudentUserId);
        var result = await teacher.SendAsync(opened.Id, "Yeniden merhaba");

        Assert.True(result.IsSuccess);
        Assert.False(Assert.Single(await teacher.InboxAsync()).IsReadOnly);
    }

    /// <summary>Katılımcı olmayan, ilişkinin durumunu öğrenmez: önce katılımcılık sorulur.</summary>
    [Fact]
    public async Task Stranger_Gets_Not_Participant_Before_Any_Relationship_Check()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        var opened = await teacher.OpenStudentAsync(StudentId);
        teacher.EndTeacherLink(StudentId);
        var queriesBefore = teacher.Students.ScopeQueryCount;

        var result = await teacher.As(StrangerUserId, "Teacher").SendAsync(opened.Id, "Merhaba");

        Assert.Equal("messaging.not_participant", result.Error.Code);
        Assert.Equal(queriesBefore, teacher.Students.ScopeQueryCount);
    }

    /// <summary>K-17: sınıf bağı (P22) birebir mesaj yetkisi vermez — kapı <c>!= None</c> değil <c>== Private</c>.</summary>
    [Fact]
    public async Task Class_Scope_Neither_Opens_Nor_Keeps_A_Conversation()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        var opened = await teacher.OpenStudentAsync(StudentId);

        teacher.EndTeacherLink(StudentId);
        teacher.Students.ClassMembers.Add((TeacherUserId, StudentId));
        teacher.Students.Owners[OtherStudentId] = OtherStudentUserId;
        teacher.Students.ClassMembers.Add((TeacherUserId, OtherStudentId));

        var send = await teacher.SendAsync(opened.Id, "Merhaba");
        var open = await teacher.OpenAsync(OtherStudentUserId, nameof(ParticipantRole.Student), OtherStudentId);

        Assert.Equal("messaging.relationship_ended", send.Error.Code);
        Assert.Equal("messaging.relationship_required", open.Error.Code);
    }

    /// <summary>Öğrencinin hesabı profilden ayrıldıysa (ör. KVKK silme) karşı taraf artık o öğrenci değildir.</summary>
    [Fact]
    public async Task Conversation_Is_Read_Only_When_The_Student_Account_Is_Gone()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        var opened = await teacher.OpenStudentAsync(StudentId);

        teacher.Students.Owners[StudentId] = null;
        var result = await teacher.SendAsync(opened.Id, "Merhaba");

        Assert.Equal("messaging.relationship_ended", result.Error.Code);
    }

    /// <summary>K-28 yalnız gönderimi kapatır: okundu, engelleme ve kendi mesajını silme açık kalır.</summary>
    [Fact]
    public async Task Read_Block_And_Delete_Stay_Available_After_The_Relationship_Ends()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        var opened = await teacher.OpenStudentAsync(StudentId);
        var message = await teacher.SendAsync(opened.Id, "Merhaba");
        var student = teacher.As(StudentUserId, "Student");

        teacher.EndTeacherLink(StudentId);
        var read = await student.MarkReadAsync(opened.Id);
        var blocked = await student.BlockAsync(opened.Id, blocked: true);
        var deleted = await teacher.DeleteAsync(message.Value!.Id);

        Assert.True(read.IsSuccess);
        Assert.True(blocked.IsSuccess);
        Assert.True(blocked.Value!.IsReadOnly);
        Assert.True(deleted.IsSuccess);
        Assert.NotNull(teacher.Repository.Messages.Single().ReadOnUtc);
    }

    /// <summary>
    /// Gelen kutusu her satır için ilişkiyi sorar; aynı (öğretmen, öğrenci) sorusu sayfa içinde bir kez sorulur —
    /// öğrenciyle ve velisiyle iki sohbeti olan öğretmen için kapsam bir kez okunur.
    /// </summary>
    [Fact]
    public async Task Inbox_Asks_Each_Teacher_Student_Pair_Once()
    {
        var teacher = new Fixture(TeacherUserId, "Teacher");
        teacher.Link(StudentId, StudentUserId);
        teacher.ApproveParent(StudentId);
        await teacher.OpenStudentAsync(StudentId);
        await teacher.OpenParentAsync(StudentId);
        var queriesBefore = teacher.Students.ScopeQueryCount;

        var inbox = await teacher.InboxAsync();

        Assert.Equal(2, inbox.Count);
        Assert.All(inbox, row => Assert.False(row.IsReadOnly));
        Assert.Equal(queriesBefore + 1, teacher.Students.ScopeQueryCount);
    }

    /// <summary>
    /// #189 çakışma yolu: kopya sohbet açılışta oluşur (mesajsız, <c>LastMessageOnUtc = açılış</c>, önizleme yok).
    /// Mesajsız ama daha yeni bir kopya kanoniğin son mesajını <c>null</c> önizlemeyle ezmemeli ve sıralamasını
    /// öne çekmemeli; engeli yine de korunmalı. Mesajlı daha yeni kopyanın son mesajı alınır.
    /// </summary>
    [Fact]
    public void Absorb_Takes_The_Newer_Last_Message_But_Ignores_An_Empty_Copy()
    {
        var canonical = Conversation.Create(
            Guid.NewGuid(), ParticipantRole.Teacher, TeacherUserId, ParticipantRole.Parent, ParentUserId, OtherStudentId, Now).Value!;
        canonical.SendMessage(Guid.NewGuid(), TeacherUserId, "Kanonik mesaj", Now);
        var emptyCopy = Conversation.Create(
            Guid.NewGuid(), ParticipantRole.Teacher, TeacherUserId, ParticipantRole.Parent, ParentUserId, StudentId, Now.AddMinutes(5)).Value!;
        emptyCopy.Block(ParentUserId, Now.AddMinutes(6));

        canonical.Absorb(emptyCopy, copyHasMessages: false);

        Assert.Equal("Kanonik mesaj", canonical.LastMessagePreview);
        Assert.Equal(Now, canonical.LastMessageOnUtc);
        Assert.True(canonical.IsBlockedByCounterpart);

        var fullCopy = Conversation.Create(
            Guid.NewGuid(), ParticipantRole.Teacher, TeacherUserId, ParticipantRole.Parent, ParentUserId, StudentId, Now).Value!;
        fullCopy.SendMessage(Guid.NewGuid(), ParentUserId, "Daha yeni", Now.AddMinutes(10));

        canonical.Absorb(fullCopy, copyHasMessages: true);

        Assert.Equal("Daha yeni", canonical.LastMessagePreview);
        Assert.Equal(Now.AddMinutes(10), canonical.LastMessageOnUtc);
    }

    /// <summary>Aynı depo ve dizinleri paylaşan, kullanıcısı değiştirilebilen kurulum (sohbetin iki tarafı tek testte).</summary>
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

        public void Link(Guid studentId, Guid studentUserId)
        {
            Students.Links.Add((TeacherUserId, studentId));
            Students.Owners[studentId] = studentUserId;
        }

        public void EndTeacherLink(Guid studentId) => Students.Links.Remove((TeacherUserId, studentId));

        public void ApproveParent(Guid studentId) => Parents.Approved.Add((ParentUserId, studentId));

        public void RevokeParent(Guid studentId) => Parents.Approved.Remove((ParentUserId, studentId));

        public async Task<ConversationResponse> OpenStudentAsync(Guid studentId)
        {
            var result = await OpenAsync(Guid.Empty, nameof(ParticipantRole.Student), studentId);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            return result.Value!;
        }

        public async Task<ConversationResponse> OpenParentAsync(Guid studentId)
        {
            var result = await OpenAsync(ParentUserId, nameof(ParticipantRole.Parent), studentId);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            return result.Value!;
        }

        public Task<Result<ConversationResponse>> OpenAsync(Guid counterpartUserId, string role, Guid studentId)
            => new OpenConversationCommandHandler(
                    CurrentUser(), Repository, Policy(), new MessagingClock(Now), new MessagingIdGenerator())
                .Handle(new OpenConversationCommand(counterpartUserId, role, studentId), CancellationToken.None);

        public Task<Result<MessageResponse>> SendAsync(Guid conversationId, string body)
            => new SendMessageCommandHandler(
                    CurrentUser(), Repository, Policy(), new MessagingClock(Now), new MessagingIdGenerator())
                .Handle(new SendMessageCommand(conversationId, body), CancellationToken.None);

        public Task<Result<IReadOnlyList<MessageResponse>>> ListMessagesAsync(Guid conversationId)
            => new ListMessagesQueryHandler(CurrentUser(), Repository)
                .Handle(new ListMessagesQuery(conversationId, null, 20), CancellationToken.None);

        public async Task<IReadOnlyList<ConversationSummaryResponse>> InboxAsync()
        {
            var result = await new ListConversationsQueryHandler(CurrentUser(), Repository, Policy())
                .Handle(new ListConversationsQuery(0, 20), CancellationToken.None);
            Assert.True(result.IsSuccess);
            return result.Value!;
        }

        public Task<Result> MarkReadAsync(Guid conversationId)
            => new MarkConversationReadCommandHandler(CurrentUser(), Repository, new MessagingClock(Now))
                .Handle(new MarkConversationReadCommand(conversationId, Repository.Messages.Where(m => m.ConversationId == conversationId).OrderByDescending(m => m.SentOnUtc).FirstOrDefault()?.Id), CancellationToken.None);

        public Task<Result<ConversationResponse>> BlockAsync(Guid conversationId, bool blocked)
            => new SetConversationBlockCommandHandler(CurrentUser(), Repository, Policy(), new MessagingClock(Now))
                .Handle(new SetConversationBlockCommand(conversationId, blocked), CancellationToken.None);

        public Task<Result> DeleteAsync(Guid messageId)
            => new DeleteMessageCommandHandler(CurrentUser(), Repository, new MessagingClock(Now))
                .Handle(new DeleteMessageCommand(messageId), CancellationToken.None);

        private FakeMessagingCurrentUser CurrentUser() => new(_userId, _roles);

        private ConversationRelationshipPolicy Policy() => new(Students, Students, Parents);
    }
}
