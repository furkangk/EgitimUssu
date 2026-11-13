using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P15 Task 1 / N-20: Öğretmen–öğrenci ve veli davet kodları güvenli üretilir, 7 gün geçerlidir,
/// yalnız doğru rol tarafından ve daveti üretmeyen biri tarafından kullanılabilir; başarısız
/// denemeler kullanıcı başına sınırlanır.
/// </summary>
public sealed class InviteCodeHardeningTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

    // ------------------------------------------------------------------ üretim

    [Fact]
    public async Task InviteStudent_Uses_Secure_Code_And_Seven_Day_Expiry()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var link = new TeacherStudentLink(Guid.NewGuid(), teacherId, studentId, TeacherStudentLinkStatus.Manual, Now);
        var handler = new InviteStudentCommandHandler(new LinkRepo(link), new FixedCodeGenerator("ABCD2345"), new FixedClock(Now));

        var result = await handler.Handle(new InviteStudentCommand(teacherId, studentId, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("ABCD2345", link.InviteCode);
        Assert.Equal(Now.AddDays(InvitePolicy.ExpiryDays), link.InviteExpiresOnUtc);
    }

    [Fact]
    public async Task CreateParentInvite_Uses_Secure_Code_And_Seven_Day_Expiry()
    {
        var studentId = Guid.NewGuid();
        var profile = NewManualProfile(studentId, Guid.NewGuid());
        var invites = new ParentInviteRepo();
        var handler = new CreateParentInviteCommandHandler(
            invites, new ProfileRepo(profile), new FixedCodeGenerator("WXYZ6789"), new SeqIdGen(), new FixedClock(Now));

        var result = await handler.Handle(new CreateParentInviteCommand(studentId, Guid.NewGuid(), "Ayşe"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("WXYZ6789", result.Value!.InviteCode);
        var stored = Assert.Single(invites.Added);
        Assert.Equal(Now.AddDays(InvitePolicy.ExpiryDays), stored.ExpiresOnUtc);
    }

    [Fact]
    public void ParentInvite_Is_Expired_After_Its_Expiry()
    {
        var invite = new StudentParentInvite(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "WXYZ6789", null, Now, Now.AddDays(7));

        Assert.False(invite.IsExpired(Now.AddDays(7).AddSeconds(-1)));
        Assert.True(invite.IsExpired(Now.AddDays(7)));
    }

    // ------------------------------------------------------------------ öğrenci claim

    [Fact]
    public async Task StudentClaim_Expired_Invite_Fails_And_Counts_As_Attempt()
    {
        var (link, _, profileRepo) = InviteSentLink(expiresOnUtc: Now.AddMinutes(-1));
        var limiter = new FakeLimiter();
        var handler = new ClaimStudentLinkCommandHandler(new LinkRepo(link), profileRepo, new ParentInviteRepo(), limiter, new FixedClock(Now));

        var result = await handler.Handle(new ClaimStudentLinkCommand("ABCD2345", Guid.NewGuid()), default);

        Assert.True(result.IsFailure);
        Assert.Equal("students.invite_expired", result.Error.Code);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
        Assert.Equal(1, limiter.Failures);
    }

    [Fact]
    public async Task StudentClaim_By_The_Inviting_Teacher_Fails()
    {
        var (link, teacherId, profileRepo) = InviteSentLink(expiresOnUtc: Now.AddDays(1));
        var handler = new ClaimStudentLinkCommandHandler(new LinkRepo(link), profileRepo, new ParentInviteRepo(), new FakeLimiter(), new FixedClock(Now));

        var result = await handler.Handle(new ClaimStudentLinkCommand("ABCD2345", teacherId), default);

        Assert.True(result.IsFailure);
        Assert.Equal("students.cannot_claim_own_invite", result.Error.Code);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
    }

    [Fact]
    public async Task StudentClaim_Normalizes_Lowercase_Code_And_Resets_Attempts_On_Success()
    {
        var (link, _, profileRepo) = InviteSentLink(expiresOnUtc: Now.AddDays(1));
        var limiter = new FakeLimiter();
        var handler = new ClaimStudentLinkCommandHandler(new LinkRepo(link), profileRepo, new ParentInviteRepo(), limiter, new FixedClock(Now));

        var result = await handler.Handle(new ClaimStudentLinkCommand(" abcd2345 ", Guid.NewGuid()), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(TeacherStudentLinkStatus.Linked, link.Status);
        Assert.Equal(1, limiter.Resets);
    }

    [Fact]
    public async Task StudentClaim_When_Blocked_Fails_Before_Looking_Up_The_Code()
    {
        var (link, _, profileRepo) = InviteSentLink(expiresOnUtc: Now.AddDays(1));
        var linkRepo = new LinkRepo(link);
        var handler = new ClaimStudentLinkCommandHandler(linkRepo, profileRepo, new ParentInviteRepo(), new FakeLimiter { Blocked = true }, new FixedClock(Now));

        var result = await handler.Handle(new ClaimStudentLinkCommand("ABCD2345", Guid.NewGuid()), default);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.too_many_attempts", result.Error.Code);
        Assert.Equal(0, linkRepo.CodeLookups);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
    }

    [Fact]
    public async Task StudentClaim_Unknown_Code_Counts_As_Attempt()
    {
        var limiter = new FakeLimiter();
        var handler = new ClaimStudentLinkCommandHandler(new LinkRepo(null), new ProfileRepo(null), new ParentInviteRepo(), limiter, new FixedClock(Now));

        var result = await handler.Handle(new ClaimStudentLinkCommand("ZZZZ9999", Guid.NewGuid()), default);

        Assert.Equal("students.invite_not_found", result.Error.Code);
        Assert.Equal(1, limiter.Failures);
    }

    [Theory]
    [InlineData("Student", true)]
    [InlineData("Admin", true)]
    [InlineData("Teacher", false)]
    [InlineData("Parent", false)]
    public async Task StudentClaim_Requires_Student_Role(string role, bool allowed)
    {
        var authorizer = new TeacherStudentLinkResponseAuthorizer(
            new FakeCurrentUser(Guid.NewGuid(), role), new LinkRepo(null));

        var result = await authorizer.Authorize(new ClaimStudentLinkCommand("ABCD2345", Guid.NewGuid()), default);

        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
        {
            Assert.Equal("students.claim_requires_student_role", result.Error.Code);
        }
    }

    // ------------------------------------------------------------------ yardımcılar

    private static StudentProfile NewManualProfile(Guid studentId, Guid teacherId)
        => new(studentId, null, teacherId, null, "Ali", "9", null, null, null, null, StudentOrigin.TeacherManaged, true, Now);

    private static (TeacherStudentLink Link, Guid TeacherId, ProfileRepo Profiles) InviteSentLink(DateTime expiresOnUtc)
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var link = new TeacherStudentLink(Guid.NewGuid(), teacherId, studentId, TeacherStudentLinkStatus.Manual, Now.AddDays(-8));
        link.MarkInviteSent("ABCD2345", null, expiresOnUtc, Now.AddDays(-8));
        return (link, teacherId, new ProfileRepo(NewManualProfile(studentId, teacherId)));
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class SeqIdGen : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FixedCodeGenerator(string code) : ISecureCodeGenerator
    {
        public string NewCode(int length = 8) => code;
    }

    private sealed class FakeLimiter : IInviteClaimAttemptLimiter
    {
        public bool Blocked { get; init; }
        public int Failures { get; private set; }
        public int Resets { get; private set; }

        public Task<bool> IsBlockedAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Blocked);

        public Task RecordFailureAsync(Guid userId, CancellationToken cancellationToken)
        {
            Failures++;
            return Task.CompletedTask;
        }

        public Task ResetAsync(Guid userId, CancellationToken cancellationToken)
        {
            Resets++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUser(Guid userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId.ToString();
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsAuthenticated => true;
    }

    private sealed class LinkRepo(TeacherStudentLink? link) : ITeacherStudentLinkRepository
    {
        public int CodeLookups { get; private set; }

        public Task<TeacherStudentLink?> GetByInviteCodeAsync(string inviteCode, CancellationToken ct)
        {
            CodeLookups++;
            return Task.FromResult(link is not null && link.InviteCode == inviteCode ? link : null);
        }

        public Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken ct)
            => Task.FromResult(link is not null && link.TeacherUserId == teacherUserId && link.StudentId == studentId ? link : null);

        public Task<TeacherStudentLink?> GetByIdAsync(Guid linkId, CancellationToken ct) => Task.FromResult(link?.Id == linkId ? link : null);
        public Task AddAsync(TeacherStudentLink link, CancellationToken ct) => Task.CompletedTask;
        public Task<int> CountByTeacherAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<TeacherStudentLink>> ListByTeacherAsync(Guid teacherUserId, bool includeArchived, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<TeacherStudentLink>> ListByStudentAsync(Guid studentId, CancellationToken ct) => throw new NotImplementedException();
        public void Remove(TeacherStudentLink link) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ProfileRepo(StudentProfile? profile) : IStudentProfileRepository
    {
        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken ct)
            => Task.FromResult(profile is not null && profile.Id == studentId ? profile : null);
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult<StudentProfile?>(null);
        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(StudentProfile profile, CancellationToken ct) => throw new NotImplementedException();
        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ParentInviteRepo : IStudentParentInviteRepository
    {
        public List<StudentParentInvite> Added { get; } = [];

        public Task AddAsync(StudentParentInvite invite, CancellationToken cancellationToken)
        {
            Added.Add(invite);
            return Task.CompletedTask;
        }

        public Task<StudentParentInvite?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StudentParentInvite?> GetByIdAsync(Guid inviteId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentParentInvite>> ListPendingByStudentAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<StudentParentInvite>>(Added.Where(invite => invite.StudentId == studentId).ToArray());
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
