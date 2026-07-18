using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class StudentClaimTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class ClaimLinkRepo : ITeacherStudentLinkRepository
    {
        private readonly TeacherStudentLink? _byCode;
        public ClaimLinkRepo(TeacherStudentLink? byCode) => _byCode = byCode;
        public int SaveCount { get; private set; }

        public Task<TeacherStudentLink?> GetByInviteCodeAsync(string inviteCode, CancellationToken ct)
            => Task.FromResult(_byCode is not null && _byCode.InviteCode == inviteCode ? _byCode : null);

        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task AddAsync(TeacherStudentLink link, CancellationToken ct) => Task.CompletedTask;
        public Task<TeacherStudentLink?> GetByIdAsync(Guid linkId, CancellationToken ct) => Task.FromResult<TeacherStudentLink?>(null);
        public Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken ct) => Task.FromResult<TeacherStudentLink?>(null);
        public Task<int> CountByTeacherAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<TeacherStudentLink>> ListByTeacherAsync(Guid teacherUserId, bool includeArchived, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class ClaimProfileRepo : IStudentProfileRepository
    {
        private readonly StudentProfile? _byId;
        private readonly StudentProfile? _byUserId;
        public ClaimProfileRepo(StudentProfile? byId, StudentProfile? byUserId)
        {
            _byId = byId;
            _byUserId = byUserId;
        }

        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken ct)
            => Task.FromResult(_byId is not null && _byId.Id == studentId ? _byId : null);
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult(_byUserId);
        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(StudentProfile profile, CancellationToken ct) => throw new NotImplementedException();
        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void MarkInviteSent_StoresCode()
    {
        var link = new TeacherStudentLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TeacherStudentLinkStatus.Manual, Now);
        link.MarkInviteSent("123456", null, Now);
        Assert.Equal("123456", link.InviteCode);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
    }

    [Fact]
    public async Task Claim_ValidCode_NoExistingProfile_LinksUserToManualProfile()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var claimingUserId = Guid.NewGuid();
        var link = new TeacherStudentLink(Guid.NewGuid(), teacherId, studentId, TeacherStudentLinkStatus.Manual, Now);
        link.MarkInviteSent("123456", null, Now);
        var manualProfile = new StudentProfile(studentId, null, teacherId, null, "Ali", "9", null, null, null, null, StudentOrigin.TeacherManaged, true, Now);

        var linkRepo = new ClaimLinkRepo(link);
        var profileRepo = new ClaimProfileRepo(manualProfile, byUserId: null);
        var handler = new ClaimStudentLinkCommandHandler(linkRepo, profileRepo, new FakeClock());

        var result = await handler.Handle(new ClaimStudentLinkCommand("123456", claimingUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TeacherStudentLinkStatus.Linked, link.Status);
        Assert.Equal(claimingUserId, manualProfile.UserId);
    }

    [Fact]
    public async Task Claim_UnknownCode_ReturnsInviteNotFound()
    {
        var linkRepo = new ClaimLinkRepo(null);
        var profileRepo = new ClaimProfileRepo(null, null);
        var handler = new ClaimStudentLinkCommandHandler(linkRepo, profileRepo, new FakeClock());

        var result = await handler.Handle(new ClaimStudentLinkCommand("000000", Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("students.invite_not_found", result.Error.Code);
    }

    [Fact]
    public async Task Claim_LinkNotInviteSent_ReturnsInviteInvalid()
    {
        var link = new TeacherStudentLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TeacherStudentLinkStatus.Manual, Now);
        // Manuel durumda kod atanmış ama davet gönderilmemiş varsay: kodu doğrudan set edemeyiz,
        // bu yüzden InviteSent yapıp ardından Accept ile Linked'e çekerek "artık geçersiz" durumu kurarız.
        link.MarkInviteSent("123456", null, Now);
        link.Accept(Now);

        var linkRepo = new ClaimLinkRepo(link);
        var profileRepo = new ClaimProfileRepo(null, null);
        var handler = new ClaimStudentLinkCommandHandler(linkRepo, profileRepo, new FakeClock());

        var result = await handler.Handle(new ClaimStudentLinkCommand("123456", Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("students.invite_invalid", result.Error.Code);
    }
}
