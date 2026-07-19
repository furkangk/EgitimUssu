using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class ParentPrimaryLinkTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Approve_RaisesConnectionNotice_WithExistingPrimary()
    {
        var existingPrimary = Guid.NewGuid();
        var link = new ParentChildLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ayşe", "baba", null, false, Now);

        link.Approve(approvedByUserId: existingPrimary, existingPrimaryParentUserId: existingPrimary, Now);

        Assert.Contains(link.DomainEvents, e => e is ParentLinkConnectionNoticeDomainEvent);
        var notice = link.DomainEvents.OfType<ParentLinkConnectionNoticeDomainEvent>().Single();
        Assert.Equal(existingPrimary, notice.ExistingPrimaryParentUserId);
    }

    [Fact]
    public async Task Approve_SecondPrimary_ByNonPrimary_Fails()
    {
        var studentId = Guid.NewGuid();
        var firstPrimaryParent = Guid.NewGuid();
        var firstLink = new ParentChildLink(Guid.NewGuid(), firstPrimaryParent, studentId, "Ayşe", "anne", null, true, Now);
        firstLink.Approve(firstPrimaryParent, null, Now);

        var secondParent = Guid.NewGuid();
        var secondLink = new ParentChildLink(Guid.NewGuid(), secondParent, studentId, "Ayşe", "baba", null, true, Now);

        var repo = new FakeRepo(secondLink, new[] { firstLink });
        var handler = new ApproveChildLinkCommandHandler(repo, new FixedClock(Now));

        // İkinci bağı, mevcut birincil olmayan biri (kendisi) onaylatmaya çalışır:
        var result = await handler.Handle(new ApproveChildLinkCommand(secondLink.Id, secondParent), default);

        Assert.True(result.IsFailure);
        Assert.Equal("parents.primary_exists", result.Error.Code);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeRepo : IParentRepository
    {
        private readonly ParentChildLink _link;
        private readonly IReadOnlyCollection<ParentChildLink> _approved;
        public FakeRepo(ParentChildLink link, IReadOnlyCollection<ParentChildLink> approved)
        { _link = link; _approved = approved; }

        public Task<ParentChildLink?> GetLinkByIdAsync(Guid linkId, CancellationToken ct) => Task.FromResult<ParentChildLink?>(_link);
        public Task<IReadOnlyCollection<ParentChildLink>> ListApprovedLinksForStudentAsync(Guid studentId, CancellationToken ct) => Task.FromResult(_approved);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<ParentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult<ParentProfile?>(null);
        public Task<ParentChildLink?> GetActiveLinkAsync(Guid parentUserId, Guid studentId, CancellationToken ct) => Task.FromResult<ParentChildLink?>(null);
        public Task<IReadOnlyCollection<ParentChildLink>> ListLinksByParentAsync(Guid parentUserId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ParentChildLink>>(Array.Empty<ParentChildLink>());
        public Task<ChildProgressSnapshot?> GetSnapshotAsync(Guid studentId, CancellationToken ct) => Task.FromResult<ChildProgressSnapshot?>(null);
        public Task<KnownStudent?> GetKnownStudentAsync(Guid studentId, CancellationToken ct) => Task.FromResult<KnownStudent?>(null);
        public Task AddProfileAsync(ParentProfile profile, CancellationToken ct) => Task.CompletedTask;
        public Task AddLinkAsync(ParentChildLink link, CancellationToken ct) => Task.CompletedTask;
    }
}
