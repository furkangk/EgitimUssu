using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Tests.Unit;

public sealed class ParentMembershipTierTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    private static ParentProfile New()
        => new(Guid.NewGuid(), Guid.NewGuid(), "Veli", null, null, Now);

    [Fact]
    public void NewProfile_DefaultsToFreeMembership()
    {
        Assert.Equal(MembershipTier.Free, New().MembershipTier);
    }

    [Fact]
    public void SetMembershipTier_UpdatesValueAndTimestamp()
    {
        var profile = New();
        var later = Now.AddMinutes(5);

        profile.SetMembershipTier(MembershipTier.Premium, later);

        Assert.Equal(MembershipTier.Premium, profile.MembershipTier);
        Assert.Equal(later, profile.UpdatedOnUtc);
    }
}
