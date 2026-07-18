using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Tests.Unit;

public sealed class MembershipGateTests
{
    [Fact]
    public void Free_History30_Premium_Unlimited()
    {
        Assert.Equal(30, MembershipGate.HistoryWindowDays(MembershipTier.Free));
        Assert.Null(MembershipGate.HistoryWindowDays(MembershipTier.Premium));
    }

    [Theory]
    [InlineData(MembershipTier.Free, PremiumFeature.MonthlyAnalysis, false)]
    [InlineData(MembershipTier.Premium, PremiumFeature.MonthlyAnalysis, true)]
    [InlineData(MembershipTier.Free, PremiumFeature.StreakFreeze, false)]
    public void Allows_ByTier(MembershipTier tier, PremiumFeature f, bool expected)
        => Assert.Equal(expected, MembershipGate.Allows(tier, f));
}
