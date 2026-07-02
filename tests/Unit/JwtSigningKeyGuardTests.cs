using EgitimUssu.Shared.Infrastructure.Configuration;

namespace EgitimUssu.Tests.Unit;

public sealed class JwtSigningKeyGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Rejects_Empty_Key(string? key)
    {
        Assert.NotNull(JwtSigningKeyGuard.Validate(key));
    }

    [Theory]
    [InlineData("change-this-development-signing-key")]
    [InlineData("replace-with-a-long-development-key")]
    public void Validate_Rejects_Known_Placeholders(string key)
    {
        Assert.NotNull(JwtSigningKeyGuard.Validate(key));
    }

    [Fact]
    public void Validate_Rejects_Key_Shorter_Than_32_Bytes()
    {
        var shortKey = new string('a', JwtSigningKeyGuard.MinimumKeyBytes - 1);
        Assert.NotNull(JwtSigningKeyGuard.Validate(shortKey));
    }

    [Fact]
    public void Validate_Accepts_Strong_Key()
    {
        var strongKey = new string('a', JwtSigningKeyGuard.MinimumKeyBytes);
        Assert.Null(JwtSigningKeyGuard.Validate(strongKey));
    }

    [Fact]
    public void EnsureValid_Throws_On_Weak_Key()
    {
        Assert.Throws<InvalidOperationException>(() => JwtSigningKeyGuard.EnsureValid("too-short"));
    }
}
