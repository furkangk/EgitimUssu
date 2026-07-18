using EgitimUssu.Modules.Students.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class StudentClaimTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkInviteSent_StoresCode()
    {
        var link = new TeacherStudentLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TeacherStudentLinkStatus.Manual, Now);
        link.MarkInviteSent("123456", null, Now);
        Assert.Equal("123456", link.InviteCode);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
    }
}
