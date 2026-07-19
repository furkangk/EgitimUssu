using EgitimUssu.Modules.Study.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class StudySessionReliabilityTests
{
    private static readonly DateTime Start = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Complete_UsesClientMinutes_WhenPlausible()
    {
        var s = StudySession.StartStopwatch(Guid.NewGuid(), Guid.NewGuid(), "Mat", null, false, false, Start);
        // 40 dk sonra tamamla; istemci 38 dk bildirdi (offline birikmiş) → 38 kabul (≤ 40+2)
        s.Complete(Start.AddMinutes(40), null, clientEffectiveMinutes: 38);
        Assert.Equal(38, s.EffectiveMinutes);
    }

    [Fact]
    public void Complete_RejectsInflatedClientMinutes()
    {
        var s = StudySession.StartStopwatch(Guid.NewGuid(), Guid.NewGuid(), "Mat", null, false, false, Start);
        s.Complete(Start.AddMinutes(40), null, clientEffectiveMinutes: 999); // > elapsed+2 → sunucu hesabı (~40)
        Assert.True(s.EffectiveMinutes <= 41);
    }

    [Fact]
    public void IsStale_TrueAfter6h()
    {
        var s = StudySession.StartStopwatch(Guid.NewGuid(), Guid.NewGuid(), "Mat", null, false, false, Start);
        Assert.True(s.IsStale(Start.AddHours(7)));
        Assert.False(s.IsStale(Start.AddHours(1)));
    }
}
