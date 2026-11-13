using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class ClaimParentInviteTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Claim_ValidCode_CreatesApprovedPrimaryLink()
    {
        var parentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var directory = new FakeInviteDirectory(Invite(studentId));
        var repo = new FakeRepo(); // no existing link/primary
        var handler = new ClaimParentInviteCommandHandler(repo, directory, new FakeLimiter(), new FixedClock(Now), new SeqIdGen());

        var result = await handler.Handle(new ClaimParentInviteCommand(parentUserId, "123456"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Approved", result.Value!.Status);
        Assert.True(result.Value!.IsPrimaryContact);
        Assert.True(directory.Claimed);
    }

    [Fact]
    public async Task Claim_InvalidCode_Fails()
    {
        var directory = new FakeInviteDirectory(null);
        var handler = new ClaimParentInviteCommandHandler(new FakeRepo(), directory, new FakeLimiter(), new FixedClock(Now), new SeqIdGen());

        var result = await handler.Handle(new ClaimParentInviteCommand(Guid.NewGuid(), "000000"), default);

        Assert.True(result.IsFailure);
        Assert.Equal("parents.invite_not_found", result.Error.Code);
    }

    /// <summary>
    /// A-05: Davet tek kullanımlıktır. Yarışı kaybeden ikinci istek — kod bu arada tüketilmişse —
    /// bağ kuramaz. Eskiden davet bağdan sonra işaretlendiği için aynı kod ikinci bir veli
    /// tarafından tekrar kullanılabiliyordu.
    /// </summary>
    [Fact]
    public async Task Claim_When_Invite_Already_Consumed_Fails_Without_Creating_A_Link()
    {
        var directory = new FakeInviteDirectory(
            Invite(Guid.NewGuid()), consumeSucceeds: false);
        var repo = new FakeRepo();
        var handler = new ClaimParentInviteCommandHandler(repo, directory, new FakeLimiter(), new FixedClock(Now), new SeqIdGen());

        var result = await handler.Handle(new ClaimParentInviteCommand(Guid.NewGuid(), "123456"), default);

        Assert.True(result.IsFailure);
        Assert.Equal("parents.invite_not_found", result.Error.Code);
        Assert.False(repo.LinkAdded);
    }

    /// <summary>
    /// A-05 telafisi: bağ kaydı düşerse davet geri verilir — veli, kendi hatası olmayan bir
    /// nedenle kodunu kaybetmemeli.
    /// </summary>
    [Fact]
    public async Task Claim_When_Link_Write_Fails_Releases_The_Invite()
    {
        var directory = new FakeInviteDirectory(Invite(Guid.NewGuid()));
        var repo = new FakeRepo { ThrowOnSave = true };
        var handler = new ClaimParentInviteCommandHandler(repo, directory, new FakeLimiter(), new FixedClock(Now), new SeqIdGen());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new ClaimParentInviteCommand(Guid.NewGuid(), "123456"), default));

        Assert.True(directory.Claimed);
        Assert.True(directory.Released);
    }

    // ------------------------------------------------------------------ P15 Task 1 / N-20

    [Fact]
    public async Task Claim_Expired_Invite_Fails_Without_Consuming_And_Counts_As_Attempt()
    {
        var directory = new FakeInviteDirectory(Invite(Guid.NewGuid(), expiresOnUtc: Now.AddSeconds(-1)));
        var limiter = new FakeLimiter();
        var handler = new ClaimParentInviteCommandHandler(new FakeRepo(), directory, limiter, new FixedClock(Now), new SeqIdGen());

        var result = await handler.Handle(new ClaimParentInviteCommand(Guid.NewGuid(), "ABCD2345"), default);

        Assert.True(result.IsFailure);
        Assert.Equal("parents.invite_expired", result.Error.Code);
        Assert.False(directory.Claimed);
        Assert.Equal(1, limiter.Failures);
    }

    [Fact]
    public async Task Claim_By_The_Inviting_Teacher_Fails()
    {
        var inviterUserId = Guid.NewGuid();
        var directory = new FakeInviteDirectory(Invite(Guid.NewGuid(), inviterUserId: inviterUserId));
        var handler = new ClaimParentInviteCommandHandler(new FakeRepo(), directory, new FakeLimiter(), new FixedClock(Now), new SeqIdGen());

        var result = await handler.Handle(new ClaimParentInviteCommand(inviterUserId, "ABCD2345"), default);

        Assert.True(result.IsFailure);
        Assert.Equal("parents.cannot_claim_own_invite", result.Error.Code);
        Assert.False(directory.Claimed);
    }

    [Fact]
    public async Task Claim_When_Blocked_Fails_Before_Resolving_The_Code()
    {
        var directory = new FakeInviteDirectory(Invite(Guid.NewGuid()));
        var handler = new ClaimParentInviteCommandHandler(new FakeRepo(), directory, new FakeLimiter { Blocked = true }, new FixedClock(Now), new SeqIdGen());

        var result = await handler.Handle(new ClaimParentInviteCommand(Guid.NewGuid(), "ABCD2345"), default);

        Assert.Equal("shared.too_many_attempts", result.Error.Code);
        Assert.Equal(0, directory.Resolves);
        Assert.False(directory.Claimed);
    }

    [Fact]
    public async Task Claim_Unknown_Code_Counts_As_Attempt_And_Success_Resets()
    {
        var limiter = new FakeLimiter();
        var missing = new ClaimParentInviteCommandHandler(new FakeRepo(), new FakeInviteDirectory(null), limiter, new FixedClock(Now), new SeqIdGen());
        await missing.Handle(new ClaimParentInviteCommand(Guid.NewGuid(), "ZZZZ9999"), default);

        var found = new ClaimParentInviteCommandHandler(new FakeRepo(), new FakeInviteDirectory(Invite(Guid.NewGuid())), limiter, new FixedClock(Now), new SeqIdGen());
        var result = await found.Handle(new ClaimParentInviteCommand(Guid.NewGuid(), "ABCD2345"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, limiter.Failures);
        Assert.Equal(1, limiter.Resets);
    }

    [Fact]
    public async Task Claim_Normalizes_The_Code_Before_Resolving()
    {
        var directory = new FakeInviteDirectory(Invite(Guid.NewGuid()));
        var handler = new ClaimParentInviteCommandHandler(new FakeRepo(), directory, new FakeLimiter(), new FixedClock(Now), new SeqIdGen());

        await handler.Handle(new ClaimParentInviteCommand(Guid.NewGuid(), " abcd2345 "), default);

        Assert.Equal("ABCD2345", directory.LastResolvedCode);
    }

    [Theory]
    [InlineData("Parent", true)]
    [InlineData("Admin", true)]
    [InlineData("Teacher", false)]
    [InlineData("Student", false)]
    public async Task Claim_Requires_Parent_Role(string role, bool allowed)
    {
        var userId = Guid.NewGuid();
        var authorizer = new ParentAuthorizer(new FakeCurrentUser(userId, role), new FakeRepo());

        var result = await authorizer.Authorize(new ClaimParentInviteCommand(userId, "ABCD2345"), default);

        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
        {
            Assert.Equal("parents.claim_requires_parent_role", result.Error.Code);
        }
    }

    private static ParentInviteInfo Invite(Guid studentId, Guid? inviterUserId = null, DateTime? expiresOnUtc = null)
        => new(Guid.NewGuid(), studentId, "Ayşe", inviterUserId ?? Guid.NewGuid(), expiresOnUtc ?? Now.AddDays(7));

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

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class SeqIdGen : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FakeInviteDirectory : IParentInviteDirectory
    {
        private readonly ParentInviteInfo? _info;
        private readonly bool _consumeSucceeds;

        public bool Claimed { get; private set; }

        public bool Released { get; private set; }

        public FakeInviteDirectory(ParentInviteInfo? info, bool consumeSucceeds = true)
        {
            _info = info;
            _consumeSucceeds = consumeSucceeds;
        }

        public int Resolves { get; private set; }

        public string? LastResolvedCode { get; private set; }

        public Task<ParentInviteInfo?> ResolveAsync(string inviteCode, CancellationToken ct)
        {
            Resolves++;
            LastResolvedCode = inviteCode;
            return Task.FromResult(_info);
        }

        public Task<bool> TryConsumeAsync(Guid inviteId, Guid parentUserId, CancellationToken ct)
        {
            Claimed = _consumeSucceeds;
            return Task.FromResult(_consumeSucceeds);
        }

        public Task ReleaseAsync(Guid inviteId, CancellationToken ct)
        {
            Released = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepo : IParentRepository
    {
        public Task<ParentChildLink?> GetActiveLinkAsync(Guid parentUserId, Guid studentId, CancellationToken ct) => Task.FromResult<ParentChildLink?>(null);
        public Task<IReadOnlyCollection<ParentChildLink>> ListApprovedLinksForStudentAsync(Guid studentId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ParentChildLink>>(Array.Empty<ParentChildLink>());
        public bool ThrowOnSave { get; init; }

        public bool LinkAdded { get; private set; }

        public Task AddLinkAsync(ParentChildLink link, CancellationToken ct)
        {
            LinkAdded = true;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => ThrowOnSave
            ? throw new InvalidOperationException("bag yazilamadi")
            : Task.CompletedTask;

        public Task<ParentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult<ParentProfile?>(null);
        public Task<ParentChildLink?> GetLinkByIdAsync(Guid linkId, CancellationToken ct) => Task.FromResult<ParentChildLink?>(null);
        public Task<IReadOnlyCollection<ParentChildLink>> ListLinksByParentAsync(Guid parentUserId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ParentChildLink>>(Array.Empty<ParentChildLink>());
        public Task<ChildProgressSnapshot?> GetSnapshotAsync(Guid studentId, CancellationToken ct) => Task.FromResult<ChildProgressSnapshot?>(null);
        public Task<KnownStudent?> GetKnownStudentAsync(Guid studentId, CancellationToken ct) => Task.FromResult<KnownStudent?>(null);
        public Task AddProfileAsync(ParentProfile profile, CancellationToken ct) => Task.CompletedTask;
    }
}
