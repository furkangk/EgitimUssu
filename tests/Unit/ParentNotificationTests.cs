using EgitimUssu.Modules.Notifications.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class ParentNotificationTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Ctor_StoresFields()
    {
        var parentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var n = new ParentNotification(Guid.NewGuid(), parentUserId, studentId, ParentNotificationType.NewAssignment, "Yeni ödev", "Ali'ye yeni ödev verildi.", Now);

        Assert.Equal(parentUserId, n.ParentUserId);
        Assert.Equal(studentId, n.StudentId);
        Assert.Equal(ParentNotificationType.NewAssignment, n.Type);
        Assert.Equal("Yeni ödev", n.Title);
        Assert.Equal(Now, n.CreatedOnUtc);
    }
}
