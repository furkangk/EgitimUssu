using EgitimUssu.Modules.Study.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class StudySessionEditTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EditCompleted_ChangesMinutesAndTopic()
    {
        var s = StudySession.CreateManual(Guid.NewGuid(), Guid.NewGuid(), "Mat", "Türev", 30, Now, null, false, false, Now);
        s.EditCompleted("Mat", "İntegral", 45, "düzeltildi", Now.AddMinutes(1));
        Assert.Equal(45, s.EffectiveMinutes);
        Assert.Equal("İntegral", s.Topic);
    }

    [Fact]
    public void EditCompleted_RejectsNonCompleted()
    {
        var s = StudySession.StartStopwatch(Guid.NewGuid(), Guid.NewGuid(), "Mat", null, false, false, Now);
        Assert.Throws<InvalidOperationException>(() => s.EditCompleted("Mat", null, 10, null, Now));
    }
}
