using EgitimUssu.Modules.Membership.Application;
using EgitimUssu.Modules.Membership.Domain;
using EgitimUssu.Modules.Membership.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P09 Task 2 / K-09: <see cref="IEntitlementDirectory"/> — üyelik haklarının <b>tek</b> okuma noktası.
/// <para>
/// Doğrulanan kurallar:
/// <list type="bullet">
///   <item>Aboneliği olmayan kullanıcı Free'dir (5 öğrenci, reklam görür).</item>
///   <item>Dönemi açık abonelik Premium'dur (sınırsız öğrenci, reklamsız, PDF).</item>
///   <item>Dönemi geçmiş abonelik Free'ye düşer; <c>Cancelled</c> ama dönemi sürüyorsa <b>hâlâ Premium</b>.</item>
///   <item>Geçiş köprüsü: abonelik yokken eski profil alanı (Students/Parents) Premium diyorsa Premium.</item>
///   <item>Fail-safe: kaynak patlarsa istisna fırlamaz, Free döner (premium açılmaz ama işlem çalışır).</item>
/// </list>
/// </para>
/// </summary>
public sealed class EntitlementDirectoryTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PlanId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Without_Subscription_Should_Be_Free_With_Five_Students()
    {
        await using var dbContext = NewDbContext();
        var directory = NewDirectory(dbContext);

        var entitlement = await directory.GetAsync(UserId, CancellationToken.None);

        Assert.Equal(MembershipTier.Free, entitlement.Tier);
        Assert.Null(entitlement.EffectiveUntilUtc);
        Assert.Equal(5, entitlement.MaxStudents);
        Assert.True(entitlement.CanSeeAds);
        Assert.False(entitlement.PdfReports);
        Assert.False(entitlement.DetailedAnalytics);
        Assert.False(entitlement.RichNotifications);
    }

    [Fact]
    public async Task Active_Subscription_Should_Be_Premium_Unlimited_And_AdFree()
    {
        await using var dbContext = NewDbContext();
        await SeedAsync(dbContext, subscription => subscription.Activate(Now.AddDays(20), Now));
        var directory = NewDirectory(dbContext);

        var entitlement = await directory.GetAsync(UserId, CancellationToken.None);

        Assert.Equal(MembershipTier.Premium, entitlement.Tier);
        Assert.Equal(Now.AddDays(20), entitlement.EffectiveUntilUtc);
        Assert.Equal(int.MaxValue, entitlement.MaxStudents);
        Assert.False(entitlement.CanSeeAds);
        Assert.True(entitlement.PdfReports);
        Assert.True(entitlement.DetailedAnalytics);
        Assert.True(entitlement.RichNotifications);
    }

    [Fact]
    public async Task Expired_Period_Should_Fall_Back_To_Free()
    {
        await using var dbContext = NewDbContext();
        await SeedAsync(dbContext, subscription =>
        {
            subscription.Activate(Now.AddDays(-1), Now.AddDays(-30));
            subscription.ExpireIfDue(Now);
        });
        var directory = NewDirectory(dbContext);

        var entitlement = await directory.GetAsync(UserId, CancellationToken.None);

        Assert.Equal(MembershipTier.Free, entitlement.Tier);
        Assert.Equal(5, entitlement.MaxStudents);
    }

    [Fact]
    public async Task Cancelled_Before_Period_End_Should_Stay_Premium()
    {
        await using var dbContext = NewDbContext();
        await SeedAsync(dbContext, subscription =>
        {
            subscription.Activate(Now.AddDays(10), Now);
            subscription.Cancel(Now);
        });
        var directory = NewDirectory(dbContext);

        var entitlement = await directory.GetAsync(UserId, CancellationToken.None);

        // İptal ≠ anında kapanma: kullanıcı ödediği dönemi kaybetmez (Öğrenilenler #122).
        Assert.Equal(MembershipTier.Premium, entitlement.Tier);
        Assert.Equal(Now.AddDays(10), entitlement.EffectiveUntilUtc);
    }

    [Fact]
    public async Task Legacy_Tier_Should_Bridge_Until_Subscriptions_Exist()
    {
        await using var dbContext = NewDbContext();
        var directory = NewDirectory(dbContext, new FakeLegacyTierSource(UserId, MembershipTier.Premium));

        var entitlement = await directory.GetAsync(UserId, CancellationToken.None);

        // Geçiş köprüsü: eski profil alanları (Students/Parents) P12'de silinene kadar okunur.
        Assert.Equal(MembershipTier.Premium, entitlement.Tier);
        Assert.Equal(int.MaxValue, entitlement.MaxStudents);
    }

    [Fact]
    public async Task Subscription_Should_Win_Over_Legacy_Free()
    {
        await using var dbContext = NewDbContext();
        await SeedAsync(dbContext, subscription => subscription.Activate(Now.AddDays(5), Now));
        var directory = NewDirectory(dbContext, new FakeLegacyTierSource(UserId, MembershipTier.Free));

        var entitlement = await directory.GetAsync(UserId, CancellationToken.None);

        Assert.Equal(MembershipTier.Premium, entitlement.Tier);
    }

    [Fact]
    public async Task GetMany_Should_Answer_For_Every_Requested_User()
    {
        await using var dbContext = NewDbContext();
        await SeedAsync(dbContext, subscription => subscription.Activate(Now.AddDays(5), Now));
        var directory = NewDirectory(dbContext);

        var entitlements = await directory.GetManyAsync([UserId, OtherUserId], CancellationToken.None);

        Assert.Equal(2, entitlements.Count);
        Assert.Equal(MembershipTier.Premium, entitlements[UserId].Tier);
        Assert.Equal(MembershipTier.Free, entitlements[OtherUserId].Tier);
    }

    [Fact]
    public async Task Failing_Source_Should_Fail_Safe_To_Free()
    {
        await using var dbContext = NewDbContext();
        var directory = NewDirectory(dbContext, new ThrowingLegacyTierSource());

        var entitlement = await directory.GetAsync(UserId, CancellationToken.None);

        // Fail-safe (fail-closed değil): premium açılmaz ama çağrı hata vermez.
        Assert.Equal(MembershipTier.Free, entitlement.Tier);
    }

    [Fact]
    public async Task Snapshot_Should_Expose_Plan_Code_And_Status()
    {
        await using var dbContext = NewDbContext();
        await SeedAsync(dbContext, subscription => subscription.Activate(Now.AddDays(15), Now));
        var directory = NewDirectory(dbContext);

        var snapshot = await ((IMembershipReader)directory).GetSnapshotAsync(UserId, CancellationToken.None);

        Assert.Equal(MembershipTier.Premium, snapshot.Tier);
        Assert.Equal(SubscriptionStatus.Active, snapshot.Status);
        Assert.Equal("student-premium-monthly", snapshot.PlanCode);
        Assert.Equal(Now.AddDays(15), snapshot.EffectiveUntilUtc);
    }

    [Fact]
    public async Task Me_Query_Should_Return_Current_User_Entitlement()
    {
        var handler = new GetMyMembershipQueryHandler(
            new FakeCurrentUser { UserId = UserId.ToString() },
            new FakeMembershipReader(new UserMembershipSnapshot(
                MembershipTier.Premium, Now.AddDays(3), SubscriptionStatus.Trialing, "teacher-premium-monthly")));

        var result = await handler.Handle(new GetMyMembershipQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Premium", result.Value.Tier);
        Assert.Equal("teacher-premium-monthly", result.Value.PlanCode);
        Assert.Equal("Trialing", result.Value.Status);
        Assert.False(result.Value.Entitlement.CanSeeAds);
    }

    // ---- Yardımcılar ------------------------------------------------------------------------

    private static MembershipDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase($"entitlement-{Guid.NewGuid():N}")
            .Options;
        return new MembershipDbContext(options, new NoOpDomainEventMapper());
    }

    private static EntitlementDirectory NewDirectory(
        MembershipDbContext dbContext, params IMembershipDirectory[] legacySources)
        => new(dbContext, new FixedClock(), legacySources, NullLogger<EntitlementDirectory>.Instance);

    private static async Task SeedAsync(MembershipDbContext dbContext, Action<UserSubscription> arrange)
    {
        var plan = new SubscriptionPlan(
            PlanId, "student-premium-monthly", "Student", MembershipTier.Premium,
            BillingPeriod.Monthly, 149m, "TRY", 7, true);
        var subscription = new UserSubscription(Guid.NewGuid(), UserId, PlanId, Now.AddDays(-30));
        arrange(subscription);

        dbContext.SubscriptionPlans.Add(plan);
        dbContext.UserSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync();
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class FakeLegacyTierSource : IMembershipDirectory
    {
        private readonly Guid _userId;
        private readonly MembershipTier _tier;

        public FakeLegacyTierSource(Guid userId, MembershipTier tier)
        {
            _userId = userId;
            _tier = tier;
        }

        public Task<MembershipTier> GetTierAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(userId == _userId ? _tier : MembershipTier.Free);
    }

    private sealed class ThrowingLegacyTierSource : IMembershipDirectory
    {
        public Task<MembershipTier> GetTierAsync(Guid userId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("veritabanı yok");
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string? UserId { get; init; }
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [];
        public bool IsAuthenticated => true;
    }

    private sealed class FakeMembershipReader : IMembershipReader
    {
        private readonly UserMembershipSnapshot _snapshot;

        public FakeMembershipReader(UserMembershipSnapshot snapshot) => _snapshot = snapshot;

        public Task<UserMembershipSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(_snapshot);
    }
}
