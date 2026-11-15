using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed partial class ParentWeeklySummaryTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
    private static readonly LocalCalendar Istanbul = new("Europe/Istanbul", new FixedClock(Now));

    [Fact]
    public async Task RunAsync_ProducesWeeklySummaryForPremiumTargets_OncePerWeek()
    {
        var targets = new[]
        {
            Target(MembershipTier.Premium, weekly: true),
            Target(MembershipTier.Premium, weekly: true)
        };
        var directory = new FakeDirectory(targets);
        var repo = new FakeRepo();
        var processor = new ParentWeeklySummaryProcessor(directory, repo, new SeqIdGen(), Istanbul);

        var first = await processor.RunAsync(Now, default);
        Assert.Equal(2, first);
        Assert.Equal(2, repo.Added.Count);
        Assert.All(repo.Added, n => Assert.Equal(ParentNotificationType.WeeklySummary, n.Type));

        // Aynı hafta ikinci çağrı: dedup → yeni bildirim yok.
        var second = await processor.RunAsync(Now.AddDays(1), default);
        Assert.Equal(0, second);
        Assert.Equal(2, repo.Added.Count);
    }

    [Fact]
    public async Task RunAsync_StartsANewWeekAtTurkeyMondayMidnight()
    {
        // P16 T6 / K-19: hafta anahtarı Türkiye haftasıdır. Pazar 15 Kasım 15:00 TR (12:00Z) bir hafta; Pazartesi 16 Kasım
        // 00:30 TR (Pazar 21:30Z) yeni hafta — eskiden anahtar UTC anın ISO haftasıydı, ikinci tur özetini kaçırırdı.
        var repo = new FakeRepo();
        var processor = new ParentWeeklySummaryProcessor(
            new FakeDirectory(new[] { Target(MembershipTier.Premium, weekly: true) }), repo, new SeqIdGen(), Istanbul);

        Assert.Equal(1, await processor.RunAsync(new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc), default));
        Assert.Equal(1, await processor.RunAsync(new DateTime(2026, 11, 15, 21, 30, 0, DateTimeKind.Utc), default));
        Assert.Equal(0, await processor.RunAsync(new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc), default));
        Assert.Equal(2, repo.Added.Count);
    }

    [Fact]
    public async Task RunAsync_SkipsFreeAndPrefOff()
    {
        var targets = new[]
        {
            Target(MembershipTier.Free, weekly: true),
            Target(MembershipTier.Premium, weekly: false)
        };
        var processor = new ParentWeeklySummaryProcessor(new FakeDirectory(targets), new FakeRepo(), new SeqIdGen(), Istanbul);

        Assert.Equal(0, await processor.RunAsync(Now, default));
    }

    private static ParentStudentNotificationTarget Target(MembershipTier tier, bool weekly)
        => new(
            Guid.NewGuid(),
            new ParentNotificationTarget(
                Guid.NewGuid(),
                tier,
                new ParentNotificationPrefs(true, weekly, true, true, true)));

    private sealed class FakeDirectory : IParentNotificationDirectory
    {
        private readonly IReadOnlyCollection<ParentStudentNotificationTarget> _all;
        public FakeDirectory(IReadOnlyCollection<ParentStudentNotificationTarget> all) => _all = all;

        public Task<ParentNotificationTargetPage> ListApprovedTargetPageAsync(Guid? afterLinkId, int take, CancellationToken cancellationToken)
            => Task.FromResult(new ParentNotificationTargetPage(_all.ToArray(), null));

        public Task<IReadOnlyCollection<ParentNotificationTarget>> GetApprovedParentsForStudentAsync(Guid studentId, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<ParentNotificationTarget>>(Array.Empty<ParentNotificationTarget>());

        public Task<IReadOnlyCollection<ParentStudentNotificationTarget>> ListAllApprovedTargetsAsync(CancellationToken ct)
            => Task.FromResult(_all);
    }

    private sealed class FakeRepo : IParentNotificationRepository
    {
        public Task<int> RunWeeklySummaryBatchAsync(Func<Task<int>> write, CancellationToken cancellationToken) => write();

        public List<ParentNotification> Added { get; } = new();
        private readonly HashSet<Guid> _processed = new();

        public Task AddAsync(ParentNotification notification, CancellationToken ct)
        {
            Added.Add(notification);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ParentNotification>> ListByParentAsync(Guid parentUserId, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<ParentNotification>>(Added.Where(n => n.ParentUserId == parentUserId).ToArray());

        public Task<bool> HasProcessedAsync(Guid eventId, CancellationToken ct) => Task.FromResult(_processed.Contains(eventId));

        public async Task<IReadOnlySet<Guid>> GetProcessedAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken)
        {
            var result = new HashSet<Guid>();
            foreach (var id in eventIds) if (await HasProcessedAsync(id, cancellationToken)) result.Add(id);
            return result;
        }

        public void MarkProcessed(Guid eventId, string eventName, DateTime nowUtc) => _processed.Add(eventId);

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class SeqIdGen : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
