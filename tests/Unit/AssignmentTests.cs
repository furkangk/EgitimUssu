using EgitimUssu.Modules.Assignments.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class AssignmentTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Assignment New(AssignmentStatus status = AssignmentStatus.Completed)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Ödev", null, null, status, null, Now, null);

    [Fact]
    public void Approve_SetsApprovedAndFeedback()
    {
        var a = New();
        a.Approve("eline sağlık", Now);
        Assert.Equal(AssignmentStatus.Approved, a.Status);
        Assert.Equal("eline sağlık", a.TeacherFeedback);
    }

    [Fact]
    public void ReturnForRevision_SetsReturnedAndFeedback()
    {
        var a = New();
        a.ReturnForRevision("2. soru eksik", Now);
        Assert.Equal(AssignmentStatus.ReturnedForRevision, a.Status);
        Assert.Equal("2. soru eksik", a.TeacherFeedback);
    }

    [Fact]
    public void SubmitWork_MovesReturnedToInProgress()
    {
        var a = New(AssignmentStatus.ReturnedForRevision);
        a.SubmitWork("https://file", Now);
        Assert.Equal(AssignmentStatus.InProgress, a.Status);
    }
}
