using EgitimUssu.Modules.Membership.Domain;
using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P09 Task 1 / M17-1: Abonelik yaşam döngüsünün saf domain kuralları.
/// <para>
/// Kritik davranış — <b>iptal anında kapanma değildir</b>: kullanıcı ödediği dönemin sonuna kadar
/// erişimini korur, tier ancak <c>ExpireIfDue</c> ile Free'ye döner. Bu ayrım yanlış kurulursa
/// kullanıcı parasını ödediği hakkı erken kaybeder.
/// </para>
/// </summary>
public sealed class SubscriptionTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SubscriptionId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid PlanId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    [Fact]
    public void StartTrial_Should_Set_Trialing_And_PeriodEnd()
    {
        var subscription = NewSubscription();

        var result = subscription.StartTrial(14, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
        Assert.Equal(Now, subscription.StartedOnUtc);
        Assert.Equal(Now.AddDays(14), subscription.CurrentPeriodEndUtc);
        Assert.True(subscription.HasAccess(Now));
        Assert.Equal(MembershipTier.Premium, subscription.EffectiveTier(Now));
    }

    [Fact]
    public void Activate_After_Trial_Should_Extend_Period()
    {
        var subscription = NewSubscription();
        subscription.StartTrial(14, Now);
        var trialEnd = subscription.CurrentPeriodEndUtc;

        var result = subscription.Activate(Now.AddDays(44), Now.AddDays(14));

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.CurrentPeriodEndUtc > trialEnd);
        Assert.Equal(Now.AddDays(44), subscription.CurrentPeriodEndUtc);
    }

    [Fact]
    public void Activate_Should_Reject_Period_End_In_The_Past()
    {
        var subscription = NewSubscription();
        subscription.StartTrial(14, Now);

        var result = subscription.Activate(Now.AddDays(-1), Now);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.invalid_period_end", result.Error.Code);
        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
    }

    [Fact]
    public void Cancel_Should_Keep_Access_Until_PeriodEnd()
    {
        var subscription = NewSubscription();
        subscription.StartTrial(14, Now);
        subscription.Activate(Now.AddDays(30), Now);
        subscription.ClearDomainEvents();

        var result = subscription.Cancel(Now.AddDays(3));

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.Equal(Now.AddDays(3), subscription.CancelledOnUtc);
        Assert.False(subscription.AutoRenew);

        // Dönem sonuna kadar erişim sürer; tier değişmediği için olay da yayılmaz.
        Assert.True(subscription.HasAccess(Now.AddDays(29)));
        Assert.Equal(MembershipTier.Premium, subscription.EffectiveTier(Now.AddDays(29)));
        Assert.Empty(subscription.DomainEvents);

        // Dönem bitince erişim düşer.
        Assert.False(subscription.HasAccess(Now.AddDays(30)));
        Assert.Equal(MembershipTier.Free, subscription.EffectiveTier(Now.AddDays(30)));
    }

    [Fact]
    public void ExpireIfDue_Should_Expire_Only_After_PeriodEnd()
    {
        var subscription = NewSubscription();
        subscription.StartTrial(14, Now);
        subscription.ClearDomainEvents();

        Assert.False(subscription.ExpireIfDue(Now.AddDays(13)));
        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
        Assert.Empty(subscription.DomainEvents);

        Assert.True(subscription.ExpireIfDue(Now.AddDays(14)));
        Assert.Equal(SubscriptionStatus.Expired, subscription.Status);
        Assert.Equal(MembershipTier.Free, subscription.EffectiveTier(Now.AddDays(14)));

        var raised = Assert.Single(subscription.DomainEvents);
        var changed = Assert.IsType<MembershipChangedDomainEvent>(raised);
        Assert.Equal(MembershipTier.Free, changed.Tier);

        // İkinci tur idempotenttir: zaten süresi dolmuş abonelik yeniden olay üretmez.
        Assert.False(subscription.ExpireIfDue(Now.AddDays(20)));
        Assert.Single(subscription.DomainEvents);
    }

    [Fact]
    public void Renew_Should_Raise_MembershipChanged()
    {
        var subscription = NewSubscription();
        subscription.StartTrial(14, Now);
        subscription.Activate(Now.AddDays(30), Now);
        subscription.ClearDomainEvents();

        var result = subscription.Renew(Now.AddDays(60), Now.AddDays(30));

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(Now.AddDays(60), subscription.CurrentPeriodEndUtc);

        var raised = Assert.Single(subscription.DomainEvents);
        var changed = Assert.IsType<MembershipChangedDomainEvent>(raised);
        Assert.Equal(UserId, changed.UserId);
        Assert.Equal(MembershipTier.Premium, changed.Tier);
        Assert.Equal(Now.AddDays(60), changed.EffectiveUntilUtc);
    }

    [Fact]
    public void Trial_Should_Be_Allowed_Once_Per_User()
    {
        var subscription = NewSubscription();
        subscription.StartTrial(14, Now);
        subscription.ExpireIfDue(Now.AddDays(14));

        var second = subscription.StartTrial(14, Now.AddDays(20));

        Assert.False(second.IsSuccess);
        Assert.Equal("membership.trial_already_used", second.Error.Code);
        Assert.Equal(SubscriptionStatus.Expired, subscription.Status);
    }

    [Fact]
    public void StartTrial_Should_Reject_Non_Positive_Days()
    {
        var subscription = NewSubscription();

        var result = subscription.StartTrial(0, Now);

        Assert.False(result.IsSuccess);
        Assert.Equal("membership.invalid_trial_days", result.Error.Code);
        Assert.False(subscription.HasUsedTrial);
    }

    [Fact]
    public void MarkPastDue_Should_Keep_Access_Until_PeriodEnd()
    {
        var subscription = NewSubscription();
        subscription.StartTrial(14, Now);
        subscription.Activate(Now.AddDays(30), Now);

        var result = subscription.MarkPastDue(Now.AddDays(30));

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionStatus.PastDue, subscription.Status);
        // Ödeme alınamadı ama dönem henüz bitmedi: erişim ödenen sürenin sonuna kadar sürer.
        Assert.True(subscription.HasAccess(Now.AddDays(29)));
        Assert.False(subscription.HasAccess(Now.AddDays(30)));
    }

    [Fact]
    public void Expired_Subscription_Can_Be_Reactivated_By_Payment()
    {
        var subscription = NewSubscription();
        subscription.StartTrial(14, Now);
        subscription.ExpireIfDue(Now.AddDays(14));
        subscription.ClearDomainEvents();

        var result = subscription.Activate(Now.AddDays(44), Now.AddDays(14));

        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.HasAccess(Now.AddDays(14)));

        var changed = Assert.IsType<MembershipChangedDomainEvent>(Assert.Single(subscription.DomainEvents));
        Assert.Equal(MembershipTier.Premium, changed.Tier);
    }

    [Fact]
    public void Plan_Should_Expose_Code_Role_And_Pricing()
    {
        var plan = new SubscriptionPlan(
            PlanId, "teacher-premium-monthly", "Teacher", MembershipTier.Premium,
            BillingPeriod.Monthly, 149.90m, "TRY", trialDays: 14, isActive: true);

        Assert.Equal("teacher-premium-monthly", plan.Code);
        Assert.Equal("Teacher", plan.Role);
        Assert.Equal(MembershipTier.Premium, plan.Tier);
        Assert.Equal(BillingPeriod.Monthly, plan.Period);
        Assert.Equal(149.90m, plan.Price);
        Assert.Equal("TRY", plan.Currency);
        Assert.Equal(14, plan.TrialDays);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void Plan_Period_End_Should_Follow_Billing_Period()
    {
        var monthly = new SubscriptionPlan(
            PlanId, "teacher-premium-monthly", "Teacher", MembershipTier.Premium,
            BillingPeriod.Monthly, 149.90m, "TRY", trialDays: 0, isActive: true);
        var yearly = new SubscriptionPlan(
            Guid.NewGuid(), "teacher-premium-yearly", "Teacher", MembershipTier.Premium,
            BillingPeriod.Yearly, 1499m, "TRY", trialDays: 0, isActive: true);

        Assert.Equal(Now.AddMonths(1), monthly.NextPeriodEnd(Now));
        Assert.Equal(Now.AddYears(1), yearly.NextPeriodEnd(Now));
    }

    private static UserSubscription NewSubscription()
        => new(SubscriptionId, UserId, PlanId, Now);
}
