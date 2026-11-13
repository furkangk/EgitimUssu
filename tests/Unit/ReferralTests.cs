using EgitimUssu.Modules.Membership.Application;
using EgitimUssu.Modules.Membership.Domain;
using EgitimUssu.Modules.Membership.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P09 Task 3 / M17-3: "Arkadaşını getir" kampanyası — davet eden ve gelen kullanıcıya 1 ay premium.
/// <para>
/// Kritik kurallar: kendi kodunu kullanamama, kod başına <b>kullanıcı başına tek</b> kullanım ve ödülün
/// mevcut premium'u <b>ezmek yerine uzatması</b> (aksi hâlde ödül, ödenmiş süreyi kısaltabilirdi).
/// </para>
/// </summary>
public sealed class ReferralTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ReferrerId = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333");
    private static readonly Guid RedeemerId = Guid.Parse("bbbbbbbb-4444-4444-4444-444444444444");
    private static readonly Guid PlanId = Guid.Parse("cccccccc-5555-5555-5555-555555555555");

    [Fact]
    public async Task Referral_Code_Should_Be_Created_Once_And_Stay_Stable()
    {
        await using var dbContext = NewDbContext();

        var first = await NewCodeHandler(dbContext, ReferrerId).Handle(
            new EnsureReferralCodeCommand(), CancellationToken.None);
        var second = await NewCodeHandler(dbContext, ReferrerId).Handle(
            new EnsureReferralCodeCommand(), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Code, second.Value!.Code);
        Assert.Equal(1, await dbContext.ReferralCodes.CountAsync());
    }

    [Fact]
    public async Task Redeeming_Own_Code_Should_Fail()
    {
        await using var dbContext = NewDbContext();
        var code = await EnsureCodeAsync(dbContext, ReferrerId);

        var result = await NewRedeemHandler(dbContext, ReferrerId).Handle(
            new RedeemReferralCommand(code), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.self_referral", result.Error.Code);
    }

    [Fact]
    public async Task Redeeming_Unknown_Code_Should_Fail()
    {
        await using var dbContext = NewDbContext();

        var result = await NewRedeemHandler(dbContext, RedeemerId).Handle(
            new RedeemReferralCommand("YOKBOYLE"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.invalid_referral", result.Error.Code);
    }

    [Fact]
    public async Task Redeeming_Twice_Should_Fail()
    {
        await using var dbContext = NewDbContext();
        var code = await EnsureCodeAsync(dbContext, ReferrerId);
        await NewRedeemHandler(dbContext, RedeemerId).Handle(
            new RedeemReferralCommand(code), CancellationToken.None);

        var result = await NewRedeemHandler(dbContext, RedeemerId).Handle(
            new RedeemReferralCommand(code), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.referral_already_used", result.Error.Code);
        Assert.Equal(1, await dbContext.ReferralRedemptions.CountAsync());
    }

    [Fact]
    public async Task Redeeming_Should_Grant_One_Month_To_Both_Sides()
    {
        await using var dbContext = NewDbContext();
        var code = await EnsureCodeAsync(dbContext, ReferrerId);

        var result = await NewRedeemHandler(dbContext, RedeemerId).Handle(
            new RedeemReferralCommand(code), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Premium", result.Value.Tier);
        Assert.Equal(Now.AddMonths(1), result.Value.EffectiveUntilUtc);

        var redeemer = await dbContext.UserSubscriptions.SingleAsync(row => row.UserId == RedeemerId);
        var referrer = await dbContext.UserSubscriptions.SingleAsync(row => row.UserId == ReferrerId);
        Assert.Equal(Now.AddMonths(1), redeemer.CurrentPeriodEndUtc);
        Assert.Equal(Now.AddMonths(1), referrer.CurrentPeriodEndUtc);
        Assert.Equal(MembershipTier.Premium, referrer.EffectiveTier(Now));

        // Ödül satın alınmış bir plana bağlı değildir; abonelik plansız açılır.
        Assert.Null(redeemer.PlanId);

        var referralCode = await dbContext.ReferralCodes.SingleAsync();
        Assert.Equal(1, referralCode.RedemptionCount);
    }

    [Fact]
    public async Task Reward_Should_Extend_Existing_Premium_Instead_Of_Overwriting()
    {
        await using var dbContext = NewDbContext();
        var code = await EnsureCodeAsync(dbContext, ReferrerId);

        // Davet eden zaten 20 gün premium: ödül bunu kısaltmamalı, sonuna eklenmeli.
        dbContext.SubscriptionPlans.Add(new SubscriptionPlan(
            PlanId, "teacher-premium-monthly", "Teacher", MembershipTier.Premium,
            BillingPeriod.Monthly, 249m, "TRY", 30, isActive: true));
        var existing = new UserSubscription(Guid.NewGuid(), ReferrerId, PlanId, Now.AddDays(-10));
        existing.Activate(Now.AddDays(20), Now);
        dbContext.UserSubscriptions.Add(existing);
        await dbContext.SaveChangesAsync();

        await NewRedeemHandler(dbContext, RedeemerId).Handle(
            new RedeemReferralCommand(code), CancellationToken.None);

        var referrer = await dbContext.UserSubscriptions.SingleAsync(row => row.UserId == ReferrerId);
        Assert.Equal(Now.AddDays(20).AddMonths(1), referrer.CurrentPeriodEndUtc);
    }

    [Fact]
    public void Referral_Code_Should_Be_Deterministic_And_Unambiguous()
    {
        var code = ReferralCodeFactory.For(ReferrerId);

        Assert.Equal(ReferralCodeFactory.For(ReferrerId), code);
        Assert.NotEqual(ReferralCodeFactory.For(RedeemerId), code);
        Assert.Equal(8, code.Length);

        // Karıştırılabilir harf/rakamlar (0/O, 1/I/L) elle girilen kodda hataya yol açar.
        Assert.DoesNotContain(code, character => "OIL01".Contains(character));
    }

    // ---- Yardımcılar ------------------------------------------------------------------------

    private static MembershipDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase($"referral-{Guid.NewGuid():N}")
            .Options;
        return new MembershipDbContext(options, new NoOpDomainEventMapper());
    }

    private static async Task<string> EnsureCodeAsync(MembershipDbContext dbContext, Guid ownerUserId)
    {
        var result = await NewCodeHandler(dbContext, ownerUserId).Handle(
            new EnsureReferralCodeCommand(), CancellationToken.None);
        return result.Value!.Code;
    }

    private static EnsureReferralCodeCommandHandler NewCodeHandler(MembershipDbContext dbContext, Guid userId)
        => new(NewCurrentUser(userId), new MembershipRepository(dbContext), new FixedClock(), new FixedIdGenerator());

    private static RedeemReferralCommandHandler NewRedeemHandler(MembershipDbContext dbContext, Guid userId)
        => new(NewCurrentUser(userId), new MembershipRepository(dbContext), new FixedClock(), new FixedIdGenerator());

    private static FakeCurrentUser NewCurrentUser(Guid userId)
        => new() { UserId = userId.ToString(), Roles = ["Student"] };

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
