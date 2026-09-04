using EgitimUssu.Modules.Membership.Application;
using EgitimUssu.Modules.Membership.Domain;
using EgitimUssu.Modules.Membership.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P09 Task 3 / M17-3: Ücretsiz deneme ("ilk ay ücretsiz") ve iptal akışları.
/// <para>
/// Kritik kural — <b>deneme kullanıcı başına bir kezdir</b>: ikinci çağrı, araya iptal girse bile
/// reddedilir. Aksi hâlde kullanıcı iptal-ederek-yeniden-başlat döngüsüyle süresiz premium alırdı.
/// </para>
/// </summary>
public sealed class TrialTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid PlanId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    [Fact]
    public async Task StartTrial_Should_Create_Subscription_And_Set_Trialing()
    {
        await using var dbContext = NewDbContext();
        await SeedPlanAsync(dbContext, trialDays: 30);
        var handler = NewStartTrialHandler(dbContext);

        var result = await handler.Handle(new StartTrialCommand("student-premium-monthly"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Premium", result.Value.Tier);
        Assert.Equal("Trialing", result.Value.Status);
        Assert.Equal("student-premium-monthly", result.Value.PlanCode);
        Assert.Equal(Now.AddDays(30), result.Value.EffectiveUntilUtc);
        Assert.False(result.Value.Entitlement.CanSeeAds);

        var stored = await dbContext.UserSubscriptions.SingleAsync(row => row.UserId == UserId);
        Assert.True(stored.HasUsedTrial);
        Assert.Equal(SubscriptionStatus.Trialing, stored.Status);
    }

    [Fact]
    public async Task StartTrial_Second_Time_Should_Fail()
    {
        await using var dbContext = NewDbContext();
        await SeedPlanAsync(dbContext, trialDays: 30);
        await NewStartTrialHandler(dbContext).Handle(
            new StartTrialCommand("student-premium-monthly"), CancellationToken.None);

        var result = await NewStartTrialHandler(dbContext).Handle(
            new StartTrialCommand("student-premium-monthly"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.trial_already_used", result.Error.Code);
    }

    [Fact]
    public async Task StartTrial_After_Cancel_Should_Still_Fail()
    {
        // İptal, deneme hakkını geri vermez — aksi hâlde döngüyle süresiz premium alınırdı.
        await using var dbContext = NewDbContext();
        await SeedPlanAsync(dbContext, trialDays: 30);
        await NewStartTrialHandler(dbContext).Handle(
            new StartTrialCommand("student-premium-monthly"), CancellationToken.None);
        await NewCancelHandler(dbContext).Handle(new CancelSubscriptionCommand(), CancellationToken.None);

        var result = await NewStartTrialHandler(dbContext).Handle(
            new StartTrialCommand("student-premium-monthly"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.trial_already_used", result.Error.Code);
    }

    [Fact]
    public async Task StartTrial_With_Unknown_Plan_Should_Fail()
    {
        await using var dbContext = NewDbContext();
        await SeedPlanAsync(dbContext, trialDays: 30);

        var result = await NewStartTrialHandler(dbContext).Handle(
            new StartTrialCommand("bilinmeyen-plan"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.plan_not_found", result.Error.Code);
    }

    [Fact]
    public async Task StartTrial_On_Plan_Without_Trial_Should_Fail()
    {
        await using var dbContext = NewDbContext();
        await SeedPlanAsync(dbContext, trialDays: 0);

        var result = await NewStartTrialHandler(dbContext).Handle(
            new StartTrialCommand("student-premium-monthly"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.trial_not_available", result.Error.Code);
    }

    [Fact]
    public async Task Cancel_Should_Keep_Access_Until_Period_End()
    {
        await using var dbContext = NewDbContext();
        await SeedPlanAsync(dbContext, trialDays: 30);
        await NewStartTrialHandler(dbContext).Handle(
            new StartTrialCommand("student-premium-monthly"), CancellationToken.None);

        var result = await NewCancelHandler(dbContext).Handle(
            new CancelSubscriptionCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        // İptal ≠ anında kapanma: seviye hâlâ Premium, dönem sonu değişmedi.
        Assert.Equal("Premium", result.Value.Tier);
        Assert.Equal("Cancelled", result.Value.Status);
        Assert.Equal(Now.AddDays(30), result.Value.EffectiveUntilUtc);

        var stored = await dbContext.UserSubscriptions.SingleAsync(row => row.UserId == UserId);
        Assert.False(stored.AutoRenew);
        Assert.True(stored.HasAccess(Now.AddDays(29)));
    }

    [Fact]
    public async Task Cancel_Without_Subscription_Should_Fail()
    {
        await using var dbContext = NewDbContext();

        var result = await NewCancelHandler(dbContext).Handle(
            new CancelSubscriptionCommand(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.subscription_not_found", result.Error.Code);
    }

    // ---- Yardımcılar ------------------------------------------------------------------------

    private static MembershipDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase($"trial-{Guid.NewGuid():N}")
            .Options;
        return new MembershipDbContext(options, new NoOpDomainEventMapper());
    }

    private static async Task SeedPlanAsync(MembershipDbContext dbContext, int trialDays)
    {
        dbContext.SubscriptionPlans.Add(new SubscriptionPlan(
            PlanId, "student-premium-monthly", "Student", MembershipTier.Premium,
            BillingPeriod.Monthly, 99m, "TRY", trialDays, isActive: true));
        await dbContext.SaveChangesAsync();
    }

    private static StartTrialCommandHandler NewStartTrialHandler(MembershipDbContext dbContext)
        => new(NewCurrentUser(), new MembershipRepository(dbContext), new FixedClock(), new FixedIdGenerator());

    private static CancelSubscriptionCommandHandler NewCancelHandler(MembershipDbContext dbContext)
        => new(NewCurrentUser(), new MembershipRepository(dbContext), new FixedClock());

    private static FakeCurrentUser NewCurrentUser()
        => new() { UserId = UserId.ToString(), Roles = ["Student"] };

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class FixedIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string? UserId { get; init; }
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [];
        public bool IsAuthenticated => true;
    }
}
