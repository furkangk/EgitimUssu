using EgitimUssu.Modules.Study.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class TestResultEditTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Edit_RecomputesNet()
    {
        var t = new TestResult(Guid.NewGuid(), Guid.NewGuid(), "Mat", null, null, TestType.Subject,
            20, 10, 8, 2, 4, null, Now, false, false, Now);
        // İlk net = 10 - 8/4 = 8
        Assert.Equal(8m, t.Net);
        t.Edit("Mat", null, null, TestType.Subject, 20, 16, 4, 0, 4, null, Now, Now);
        Assert.Equal(15m, t.Net); // 16 - 4/4 = 15
    }
}
