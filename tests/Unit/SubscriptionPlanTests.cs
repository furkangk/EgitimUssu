using EgitimUssu.Modules.Membership.Application;
using EgitimUssu.Modules.Membership.Domain;
using EgitimUssu.Modules.Membership.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P09 Task 3 / M17-1: Plan kataloğu — anonim listeleme (paywall ekranı) ve Admin'e kapalı yönetim.
/// <para>
/// <c>GET /plans</c> bilinçli olarak anonimdir: paywall ekranı, kullanıcı henüz giriş yapmadan da
/// fiyatları gösterebilmelidir. Yazma uçları ise yalnız Admin'e açıktır.
/// </para>
/// </summary>
public sealed class SubscriptionPlanTests
{
    private static readonly Guid ExistingPlanId = Guid.Parse("dddddddd-6666-6666-6666-666666666666");

    [Fact]
    public async Task List_Should_Return_Only_Active_Plans_Filtered_By_Role()
    {
        await using var dbContext = NewDbContext();
        await SeedAsync(dbContext);

        var result = await new ListPlansQueryHandler(new MembershipRepository(dbContext))
            .Handle(new ListPlansQuery("Teacher"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(["teacher-premium-monthly", "teacher-premium-yearly"], result.Value.Select(plan => plan.Code));
    }

    [Fact]
    public async Task List_Without_Role_Should_Return_All_Active_Plans()
    {
        await using var dbContext = NewDbContext();
        await SeedAsync(dbContext);

        var result = await new ListPlansQueryHandler(new MembershipRepository(dbContext))
            .Handle(new ListPlansQuery(null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        // Pasif plan listeye girmez — istemci satılmayan bir planı gösteremez.
        Assert.DoesNotContain(result.Value, plan => plan.Code == "student-premium-legacy");
        Assert.Equal(3, result.Value.Count);
    }

    [Fact]
    public async Task Admin_Should_Create_Plan()
    {
        await using var dbContext = NewDbContext();
        var handler = new CreateSubscriptionPlanCommandHandler(
            new MembershipRepository(dbContext), new FixedIdGenerator());

        var result = await handler.Handle(
            new CreateSubscriptionPlanCommand(
                "parent-premium-monthly", "Parent", "Premium", "Monthly", 99m, "TRY", 30, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("parent-premium-monthly", result.Value.Code);
        Assert.Equal(99m, result.Value.Price);
        Assert.Equal(1, await dbContext.SubscriptionPlans.CountAsync());
    }

    [Fact]
    public async Task Creating_Duplicate_Plan_Code_Should_Fail()
    {
        await using var dbContext = NewDbContext();
        await SeedAsync(dbContext);
        var handler = new CreateSubscriptionPlanCommandHandler(
            new MembershipRepository(dbContext), new FixedIdGenerator());

        var result = await handler.Handle(
            new CreateSubscriptionPlanCommand(
                "teacher-premium-monthly", "Teacher", "Premium", "Monthly", 349m, "TRY", 30, true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.plan_code_conflict", result.Error.Code);
    }

    [Fact]
    public async Task Creating_Plan_With_Unknown_Role_Should_Fail()
    {
        await using var dbContext = NewDbContext();
        var handler = new CreateSubscriptionPlanCommandHandler(
            new MembershipRepository(dbContext), new FixedIdGenerator());

        var result = await handler.Handle(
            new CreateSubscriptionPlanCommand("x-premium", "Admin", "Premium", "Monthly", 10m, "TRY", 0, true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.invalid_plan", result.Error.Code);
    }

    [Fact]
    public async Task Admin_Should_Update_Price_And_Deactivate_Plan()
    {
        await using var dbContext = NewDbContext();
        await SeedAsync(dbContext);
        var handler = new UpdateSubscriptionPlanCommandHandler(new MembershipRepository(dbContext));

        var result = await handler.Handle(
            new UpdateSubscriptionPlanCommand(ExistingPlanId, 299m, "TRY", 14, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(299m, result.Value.Price);
        Assert.Equal(14, result.Value.TrialDays);
        Assert.False(result.Value.IsActive);

        var stored = await dbContext.SubscriptionPlans.SingleAsync(plan => plan.Id == ExistingPlanId);
        Assert.Equal(299m, stored.Price);

        // Kod ve rol kararlı anahtarlardır; güncelleme onları değiştirmez.
        Assert.Equal("teacher-premium-monthly", stored.Code);
        Assert.Equal("Teacher", stored.Role);
    }

    [Fact]
    public async Task Updating_Unknown_Plan_Should_Fail()
    {
        await using var dbContext = NewDbContext();
        var handler = new UpdateSubscriptionPlanCommandHandler(new MembershipRepository(dbContext));

        var result = await handler.Handle(
            new UpdateSubscriptionPlanCommand(Guid.NewGuid(), 10m, "TRY", 0, true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.plan_not_found", result.Error.Code);
    }

    [Fact]
    public async Task Plan_Writes_Should_Be_Admin_Only()
    {
        var admin = new AdminOnlyMembershipAuthorizer(
            new FakeCurrentUser { Roles = ["Admin"] });
        var teacher = new AdminOnlyMembershipAuthorizer(
            new FakeCurrentUser { Roles = ["Teacher"] });

        Assert.True((await admin.Authorize(
            new CreateSubscriptionPlanCommand("a", "Teacher", "Premium", "Monthly", 1m, "TRY", 0, true),
            CancellationToken.None)).IsSuccess);

        var denied = await teacher.Authorize(
            new UpdateSubscriptionPlanCommand(ExistingPlanId, 1m, "TRY", 0, true), CancellationToken.None);
        Assert.False(denied.IsSuccess);
        Assert.Equal("shared.forbidden", denied.Error.Code);
    }

    [Fact]
    public void Seed_Should_Cover_Three_Roles_In_Both_Periods()
    {
        // Fiyatlar codex/03_paket_fiyatlandirma.md §4 lansman hipotezinin alt sınırıdır (Q2 ile doğrulanacak).
        var plans = MembershipPlanSeedData.Plans;

        Assert.Equal(6, plans.Count);
        Assert.Equal(6, plans.Select(plan => plan.Code).Distinct().Count());
        Assert.Equal(["Parent", "Student", "Teacher"], plans.Select(plan => plan.Role).Distinct().Order());
        Assert.All(plans, plan => Assert.Equal("TRY", plan.Currency));
        Assert.All(plans, plan => Assert.True(plan.Price > 0));

        // "İlk ay ücretsiz" kampanyası: aylık planlar 30 günlük deneme taşır.
        Assert.All(
            plans.Where(plan => plan.Period == BillingPeriod.Monthly),
            plan => Assert.Equal(30, plan.TrialDays));
    }

    // ---- Yardımcılar ------------------------------------------------------------------------

    private static MembershipDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase($"plans-{Guid.NewGuid():N}")
            .Options;
        return new MembershipDbContext(options, new NoOpDomainEventMapper());
    }

    private static async Task SeedAsync(MembershipDbContext dbContext)
    {
        dbContext.SubscriptionPlans.AddRange(
            new SubscriptionPlan(
                ExistingPlanId, "teacher-premium-monthly", "Teacher", MembershipTier.Premium,
                BillingPeriod.Monthly, 249m, "TRY", 30, isActive: true),
            new SubscriptionPlan(
                Guid.NewGuid(), "teacher-premium-yearly", "Teacher", MembershipTier.Premium,
                BillingPeriod.Yearly, 2490m, "TRY", 0, isActive: true),
            new SubscriptionPlan(
                Guid.NewGuid(), "student-premium-monthly", "Student", MembershipTier.Premium,
                BillingPeriod.Monthly, 99m, "TRY", 30, isActive: true),
            new SubscriptionPlan(
                Guid.NewGuid(), "student-premium-legacy", "Student", MembershipTier.Premium,
                BillingPeriod.Monthly, 49m, "TRY", 0, isActive: false));
        await dbContext.SaveChangesAsync();
    }

    private sealed class FixedIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string? UserId { get; init; } = Guid.NewGuid().ToString();
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [];
        public bool IsAuthenticated => true;
    }
}
