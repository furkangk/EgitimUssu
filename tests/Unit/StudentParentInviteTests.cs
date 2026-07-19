using EgitimUssu.Modules.Students.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class StudentParentInviteTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Claim_SetsStatusAndParent()
    {
        var invite = new StudentParentInvite(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "123456", "Ayşe", Now);
        var parentUserId = Guid.NewGuid();

        invite.Claim(parentUserId, Now.AddMinutes(1));

        Assert.Equal(ParentInviteStatus.Claimed, invite.Status);
        Assert.Equal(parentUserId, invite.ClaimedByParentUserId);
    }

    [Fact]
    public void Claim_Twice_Throws()
    {
        var invite = new StudentParentInvite(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "123456", null, Now);
        invite.Claim(Guid.NewGuid(), Now);
        Assert.Throws<InvalidOperationException>(() => invite.Claim(Guid.NewGuid(), Now));
    }
}
