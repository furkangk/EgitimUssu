using EgitimUssu.Modules.Study.Application;

namespace EgitimUssu.Tests.Unit;

public sealed class ExamPenaltyTests
{
    [Theory]
    [InlineData("LGS", 3)]
    [InlineData("TYT", 4)]
    [InlineData("AYT", 4)]
    [InlineData("Other", 4)]
    public void DivisorFor_KnownExams(string exam, int expected)
        => Assert.Equal(expected, ExamPenalty.DivisorFor(exam));

    [Fact]
    public void DivisorFor_School_ReturnsNull() // yanlış götürmez
        => Assert.Null(ExamPenalty.DivisorFor("School"));
}
