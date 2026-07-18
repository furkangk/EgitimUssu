using EgitimUssu.Modules.Scheduling.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class TimeOffBlockTests
{
    [Fact]
    public void Ctor_StoresFields()
    {
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var block = new TimeOffBlock(Guid.NewGuid(), Guid.NewGuid(), TimeOffType.Holiday, "Yaz tatili", start, end, true, start);

        Assert.Equal("Yaz tatili", block.Title);
        Assert.Equal(TimeOffType.Holiday, block.Type);
        Assert.True(block.IsAllDay);
        Assert.Equal(start, block.StartAtUtc);
        Assert.Equal(end, block.EndAtUtc);
    }
}
