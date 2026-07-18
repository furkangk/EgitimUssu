using EgitimUssu.Modules.Scheduling.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class LessonChangeRequestTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Accept_SetsStatus_RaisesEvent()
    {
        var r = new LessonChangeRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "hastayım", Now.AddDays(1), Now.AddDays(1).AddHours(1), Now);
        r.Accept(Now.AddHours(1));
        Assert.Equal(LessonChangeRequestStatus.Accepted, r.Status);
        Assert.Contains(r.DomainEvents, e => e is LessonChangeRequestResolvedDomainEvent);
    }

    [Fact]
    public void Reject_OnlyFromPending()
    {
        var r = new LessonChangeRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "x", null, null, Now);
        r.Reject(Now);
        Assert.Throws<InvalidOperationException>(() => r.Accept(Now));
    }
}
