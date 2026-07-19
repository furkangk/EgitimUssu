using EgitimUssu.Modules.Study.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class MockExamTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MockExam_SumsNetOfSubjects()
    {
        var m = new MockExam(Guid.NewGuid(), Guid.NewGuid(), "TYT", Now, Now);
        var t1 = new TestResult(Guid.NewGuid(), m.StudentId, "Türkçe", null, null, TestType.General, 40, 30, 8, 2, 4, null, Now, false, false, Now); // 30-2=28
        var t2 = new TestResult(Guid.NewGuid(), m.StudentId, "Matematik", null, null, TestType.General, 40, 20, 4, 16, 4, null, Now, false, false, Now); // 20-1=19
        m.AddSubject(t1);
        m.AddSubject(t2);
        Assert.Equal(47m, m.TotalNet); // 28+19
        Assert.Equal(m.Id, t1.MockExamId);
        Assert.Equal(m.Id, t2.MockExamId);
    }
}
