using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class ResultTests
{
    [Fact]
    public void Success_Result_Should_BeSuccessful()
    {
        var result = Result<string>.Success("ok");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("ok", result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_Result_Should_Carry_Error()
    {
        var error = new Error("demo.error", "Something happened.");
        var result = Result<string>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }
}
