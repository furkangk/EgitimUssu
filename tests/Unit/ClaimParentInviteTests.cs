using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Modules.Parents.Domain;
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
        var directory = new FakeInviteDirectory(new ParentInviteInfo(Guid.NewGuid(), studentId, "Ayşe"));
        var repo = new FakeRepo(); // no existing link/primary
        var handler = new ClaimParentInviteCommandHandler(repo, directory, new FixedClock(Now), new SeqIdGen());

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
        var handler = new ClaimParentInviteCommandHandler(new FakeRepo(), directory, new FixedClock(Now), new SeqIdGen());

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
            new ParentInviteInfo(Guid.NewGuid(), Guid.NewGuid(), "Ayşe"), consumeSucceeds: false);
        var repo = new FakeRepo();
        var handler = new ClaimParentInviteCommandHandler(repo, directory, new FixedClock(Now), new SeqIdGen());

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
        var directory = new FakeInviteDirectory(new ParentInviteInfo(Guid.NewGuid(), Guid.NewGuid(), "Ayşe"));
        var repo = new FakeRepo { ThrowOnSave = true };
        var handler = new ClaimParentInviteCommandHandler(repo, directory, new FixedClock(Now), new SeqIdGen());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new ClaimParentInviteCommand(Guid.NewGuid(), "123456"), default));

        Assert.True(directory.Claimed);
        Assert.True(directory.Released);
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

        public Task<ParentInviteInfo?> ResolveAsync(string inviteCode, CancellationToken ct) => Task.FromResult(_info);

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
