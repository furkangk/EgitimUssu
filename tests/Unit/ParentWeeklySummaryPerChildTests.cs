using EgitimUssu.Shared.Contracts;
using EgitimUssu.Modules.Notifications.Infrastructure;
namespace EgitimUssu.Tests.Unit;
public sealed partial class ParentWeeklySummaryTests
{
    [Fact]
    public async Task Two_Children_Receive_Two_Independent_Weekly_Summaries()
    {
        var parent=Guid.NewGuid();
        var first=Target(MembershipTier.Premium,true);
        var second=Target(MembershipTier.Premium,true);
        first=first with { Target=first.Target with { ParentUserId=parent } };
        second=second with { Target=second.Target with { ParentUserId=parent } };
        var repo=new FakeRepo();
        var processor=new ParentWeeklySummaryProcessor(new FakeDirectory(new[]{first,second}),repo,new SeqIdGen(),Istanbul);
        Assert.Equal(2,await processor.RunAsync(Now));
        Assert.Equal(2,repo.Added.Select(x=>x.StudentId).Distinct().Count());
        Assert.Equal(0,await processor.RunAsync(Now.AddDays(1)));
    }

}
