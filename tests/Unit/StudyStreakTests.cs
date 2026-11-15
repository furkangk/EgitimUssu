using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class StudyStreakTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Goal_StoresStreakThresholdPercent()
    {
        var goal = new StudyGoal(Guid.NewGuid(), Guid.NewGuid(), 120, null, null, null, null, 60, Now);
        Assert.Equal(60, goal.StreakThresholdPercent);

        goal.UpdateGoals(120, null, null, null, null, 75, Now);
        Assert.Equal(75, goal.StreakThresholdPercent);
    }
}

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}

internal sealed class FakeIdGen : IIdGenerator
{
    public Guid New() => Guid.NewGuid();
}
