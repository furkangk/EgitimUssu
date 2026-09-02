using EgitimUssu.Shared.Infrastructure.Configuration;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// A-06: Postgres bağlantı dizesi sırrı repoda tutulmaz; startup guard'ı eksik/zayıf dizeyi üretimde reddeder.
/// </summary>
public sealed class ConnectionStringGuardTests
{
    [Fact]
    public void EnsureValid_Should_Throw_When_Empty_In_Production()
        => Assert.Throws<InvalidOperationException>(
            () => ConnectionStringGuard.EnsureValid("", isDevelopment: false));

    [Fact]
    public void EnsureValid_Should_Throw_When_Empty_In_Development()
        => Assert.Throws<InvalidOperationException>(
            () => ConnectionStringGuard.EnsureValid("   ", isDevelopment: true));

    [Fact]
    public void EnsureValid_Should_Throw_When_Default_Password_In_Production()
        => Assert.Throws<InvalidOperationException>(() => ConnectionStringGuard.EnsureValid(
            "Host=localhost;Database=egitimussu;Username=postgres;Password=postgres", isDevelopment: false));

    [Fact]
    public void EnsureValid_Should_Throw_When_Password_Is_Blank_In_Production()
        => Assert.Throws<InvalidOperationException>(() => ConnectionStringGuard.EnsureValid(
            "Host=db.example.com;Database=egitimussu;Username=app;Password=", isDevelopment: false));

    [Fact]
    public void EnsureValid_Should_Allow_InMemory()
        => ConnectionStringGuard.EnsureValid("InMemory:tests", isDevelopment: false);

    [Fact]
    public void EnsureValid_Should_Allow_Weak_Password_In_Development()
        => ConnectionStringGuard.EnsureValid(
            "Host=localhost;Database=egitimussu;Username=postgres;Password=postgres", isDevelopment: true);

    /// <summary>Regresyon: parola denetimi "password=" alt dizesine bakarsa her üretim dizesi reddedilirdi.</summary>
    [Fact]
    public void EnsureValid_Should_Allow_Strong_Password_In_Production()
        => ConnectionStringGuard.EnsureValid(
            "Host=db.example.com;Port=5432;Database=egitimussu;Username=app;Password=Xk7#pQ2vLm9!zTr4",
            isDevelopment: false);

    [Fact]
    public void Validate_Should_Return_Null_When_Valid()
        => Assert.Null(ConnectionStringGuard.Validate("InMemory:tests", isDevelopment: false));

    [Fact]
    public void Validate_Should_Return_Reason_When_Invalid()
        => Assert.NotNull(ConnectionStringGuard.Validate(null, isDevelopment: false));
}
