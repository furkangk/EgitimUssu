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
        public bool Claimed { get; private set; }
        public FakeInviteDirectory(ParentInviteInfo? info) => _info = info;

        public Task<ParentInviteInfo?> ResolveAsync(string inviteCode, CancellationToken ct) => Task.FromResult(_info);
        public Task MarkClaimedAsync(Guid inviteId, Guid parentUserId, CancellationToken ct)
        {
            Claimed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepo : IParentRepository
    {
        public Task<ParentChildLink?> GetActiveLinkAsync(Guid parentUserId, Guid studentId, CancellationToken ct) => Task.FromResult<ParentChildLink?>(null);
        public Task<IReadOnlyCollection<ParentChildLink>> ListApprovedLinksForStudentAsync(Guid studentId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ParentChildLink>>(Array.Empty<ParentChildLink>());
        public Task AddLinkAsync(ParentChildLink link, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<ParentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult<ParentProfile?>(null);
        public Task<ParentChildLink?> GetLinkByIdAsync(Guid linkId, CancellationToken ct) => Task.FromResult<ParentChildLink?>(null);
        public Task<IReadOnlyCollection<ParentChildLink>> ListLinksByParentAsync(Guid parentUserId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ParentChildLink>>(Array.Empty<ParentChildLink>());
        public Task<ChildProgressSnapshot?> GetSnapshotAsync(Guid studentId, CancellationToken ct) => Task.FromResult<ChildProgressSnapshot?>(null);
        public Task<KnownStudent?> GetKnownStudentAsync(Guid studentId, CancellationToken ct) => Task.FromResult<KnownStudent?>(null);
        public Task AddProfileAsync(ParentProfile profile, CancellationToken ct) => Task.CompletedTask;
    }
}
