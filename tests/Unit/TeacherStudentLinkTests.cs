using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class TeacherStudentLinkTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    private static TeacherStudentLink New(TeacherStudentLinkStatus status = TeacherStudentLinkStatus.Manual)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status, Now);

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser(Guid? userId, params string[] roles)
        {
            UserId = userId?.ToString();
            Roles = roles;
        }

        public string? UserId { get; }
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; }
        public bool IsAuthenticated => UserId is not null;
    }

    private sealed class SingleLinkRepo : ITeacherStudentLinkRepository
    {
        private readonly TeacherStudentLink? _link;
        public SingleLinkRepo(TeacherStudentLink? link) => _link = link;
        public int SaveCount { get; private set; }

        public Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken ct)
            => Task.FromResult(_link);
        public Task<TeacherStudentLink?> GetByIdAsync(Guid linkId, CancellationToken ct) => Task.FromResult(_link);
        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task AddAsync(TeacherStudentLink link, CancellationToken ct) => Task.CompletedTask;
        public Task<int> CountByTeacherAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<TeacherStudentLink>> ListByTeacherAsync(Guid teacherUserId, bool includeArchived, CancellationToken ct) => throw new NotImplementedException();
    }

    [Fact]
    public async Task SetRateAuthorizer_RejectsOtherTeacher()
    {
        var teacherId = Guid.NewGuid();
        var otherTeacher = Guid.NewGuid();
        var authorizer = new TeacherStudentLinkAuthorizer(new FakeCurrentUser(otherTeacher, "Teacher"));

        var result = await authorizer.Authorize(
            new SetTeacherStudentRateCommand(teacherId, Guid.NewGuid(), 500m, "TRY"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task SetRateAuthorizer_AllowsOwningTeacher()
    {
        var teacherId = Guid.NewGuid();
        var authorizer = new TeacherStudentLinkAuthorizer(new FakeCurrentUser(teacherId, "Teacher"));

        var result = await authorizer.Authorize(
            new SetTeacherStudentRateCommand(teacherId, Guid.NewGuid(), 500m, "TRY"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SetRateHandler_SetsRateOnLink()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var link = new TeacherStudentLink(Guid.NewGuid(), teacherId, studentId, TeacherStudentLinkStatus.Manual, Now);
        var repo = new SingleLinkRepo(link);
        var handler = new SetTeacherStudentRateCommandHandler(repo, new FakeClock());

        var result = await handler.Handle(new SetTeacherStudentRateCommand(teacherId, studentId, 600m, "TRY"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(600m, link.AgreedRateAmount);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task ArchiveHandler_MissingLink_ReturnsNotFound()
    {
        var handler = new ArchiveTeacherStudentLinkCommandHandler(new SingleLinkRepo(null), new FakeClock());

        var result = await handler.Handle(new ArchiveTeacherStudentLinkCommand(Guid.NewGuid(), Guid.NewGuid(), true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("students.link_not_found", result.Error.Code);
    }

    private sealed class SingleProfileRepo : IStudentProfileRepository
    {
        private readonly StudentProfile? _profile;
        public SingleProfileRepo(StudentProfile? profile) => _profile = profile;

        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken ct) => Task.FromResult(_profile);
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(StudentProfile profile, CancellationToken ct) => throw new NotImplementedException();
        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task InviteThenAccept_MarksLinkedAndBindsStudentUser()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var link = new TeacherStudentLink(linkId, teacherId, studentId, TeacherStudentLinkStatus.Manual, Now);
        var profile = new StudentProfile(studentId, null, teacherId, null, "Ali", "9", null, null, null, null, StudentOrigin.TeacherManaged, true, Now);
        var linkRepo = new SingleLinkRepo(link);

        var invite = new InviteStudentCommandHandler(linkRepo, new FakeClock());
        var inviteResult = await invite.Handle(new InviteStudentCommand(teacherId, studentId, studentUserId), CancellationToken.None);

        Assert.True(inviteResult.IsSuccess);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
        Assert.Equal(studentUserId, link.InviteTargetUserId);

        var accept = new AcceptTeacherStudentLinkCommandHandler(linkRepo, new SingleProfileRepo(profile), new FakeClock());
        var acceptResult = await accept.Handle(new AcceptTeacherStudentLinkCommand(linkId, studentUserId), CancellationToken.None);

        Assert.True(acceptResult.IsSuccess);
        Assert.Equal(TeacherStudentLinkStatus.Linked, link.Status);
        Assert.Equal(studentUserId, profile.UserId);
    }

    [Fact]
    public async Task RejectHandler_MissingLink_ReturnsNotFound()
    {
        var handler = new RejectTeacherStudentLinkCommandHandler(new SingleLinkRepo(null), new FakeClock());

        var result = await handler.Handle(new RejectTeacherStudentLinkCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("students.link_not_found", result.Error.Code);
    }

    [Fact]
    public void SetRate_StoresAmountAndCurrency()
    {
        var link = New();
        link.SetRate(450m, "TRY", Now);
        Assert.Equal(450m, link.AgreedRateAmount);
        Assert.Equal("TRY", link.Currency);
    }

    [Fact]
    public void ArchiveUnarchive_TogglesFlag()
    {
        var link = New();
        link.Archive(Now);
        Assert.True(link.IsArchived);
        link.Unarchive(Now);
        Assert.False(link.IsArchived);
    }

    [Fact]
    public void InviteAcceptReject_TransitionsStatus()
    {
        var target = Guid.NewGuid();
        var link = New();
        link.MarkInviteSent("123456", target, Now);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
        Assert.Equal(target, link.InviteTargetUserId);

        link.Accept(Now);
        Assert.Equal(TeacherStudentLinkStatus.Linked, link.Status);

        var link2 = New();
        link2.MarkInviteSent("654321", target, Now);
        link2.Reject(Now);
        Assert.Equal(TeacherStudentLinkStatus.Rejected, link2.Status);
    }
}
