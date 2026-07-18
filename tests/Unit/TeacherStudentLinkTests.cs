using EgitimUssu.Modules.Students.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class TeacherStudentLinkTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    private static TeacherStudentLink New(TeacherStudentLinkStatus status = TeacherStudentLinkStatus.Manual)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status, Now);

    [Fact]
    public void SetRate_StoresAmountAndCurrency()
    {
        var link = New();
        link.SetRate(450m, "TRY", Now);
        Assert.Equal(450m, link.AgreedRateAmount);
        Assert.Equal("TRY", link.Currency);
    }

    [Fact]
    public void ArchiveUnarchive_TogglesFlag()
    {
        var link = New();
        link.Archive(Now);
        Assert.True(link.IsArchived);
        link.Unarchive(Now);
        Assert.False(link.IsArchived);
    }

    [Fact]
    public void InviteAcceptReject_TransitionsStatus()
    {
        var target = Guid.NewGuid();
        var link = New();
        link.MarkInviteSent(target, Now);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
        Assert.Equal(target, link.InviteTargetUserId);

        link.Accept(Now);
        Assert.Equal(TeacherStudentLinkStatus.Linked, link.Status);

        var link2 = New();
        link2.MarkInviteSent(target, Now);
        link2.Reject(Now);
        Assert.Equal(TeacherStudentLinkStatus.Rejected, link2.Status);
    }
}
