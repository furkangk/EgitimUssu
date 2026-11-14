using EgitimUssu.Shared.Infrastructure.Configuration;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P02/K-01: E-posta konfigürasyonu startup'ta doğrulanır. SMTP seçiliyken eksik ayarla uygulama açılmaz;
/// "Logging" sağlayıcısı <b>yalnız Development/Staging'de</b> açılır (P15 T6 / N-24 — eskiden her ortamda
/// açılıyordu; ortam kuralının tamamı <see cref="ProductionProviderGuardTests"/>'te).
/// Guard, <see cref="JwtSigningKeyGuard"/>/<see cref="ConnectionStringGuard"/> desenini izler:
/// <c>Validate</c> nedeni döner, <c>EnsureValid</c> fırlatır.
/// </summary>
public sealed class EmailOptionsGuardTests
{
    [Fact]
    public void EnsureValid_Should_Throw_When_Smtp_Host_Missing()
    {
        var options = new EmailOptions { Provider = "Smtp", FromEmail = "no-reply@egitimussu.com" };

        Assert.Throws<InvalidOperationException>(() => EmailOptionsGuard.EnsureValid(options, "Production"));
    }

    [Fact]
    public void EnsureValid_Should_Throw_When_FromEmail_Missing()
    {
        var options = new EmailOptions
        {
            Provider = "Smtp",
            Smtp = new SmtpOptions { Host = "smtp.example.com", Username = "u", Password = "p" }
        };

        Assert.Throws<InvalidOperationException>(() => EmailOptionsGuard.EnsureValid(options, "Production"));
    }

    [Fact]
    public void EnsureValid_Should_Throw_When_Port_Invalid()
    {
        var options = new EmailOptions
        {
            Provider = "Smtp",
            FromEmail = "no-reply@egitimussu.com",
            Smtp = new SmtpOptions { Host = "smtp.example.com", Port = 0, Username = "u", Password = "p" }
        };

        Assert.Throws<InvalidOperationException>(() => EmailOptionsGuard.EnsureValid(options, "Production"));
    }

    [Fact]
    public void Validate_Should_Report_Missing_Username_In_Production()
    {
        var options = new EmailOptions
        {
            Provider = "Smtp",
            FromEmail = "no-reply@egitimussu.com",
            Smtp = new SmtpOptions { Host = "smtp.example.com", Port = 587 }
        };

        var error = EmailOptionsGuard.Validate(options, "Production");

        Assert.NotNull(error);
        Assert.Contains("Email__Smtp__Username", error);
    }

    [Fact]
    public void Validate_Should_Allow_Missing_Username_In_Development()
    {
        var options = new EmailOptions
        {
            Provider = "Smtp",
            FromEmail = "no-reply@egitimussu.com",
            Smtp = new SmtpOptions { Host = "localhost", Port = 1025 }
        };

        Assert.Null(EmailOptionsGuard.Validate(options, "Development"));
    }

    [Fact]
    public void Validate_Should_Allow_Complete_Smtp_Configuration()
    {
        var options = new EmailOptions
        {
            Provider = "Smtp",
            FromEmail = "no-reply@egitimussu.com",
            Smtp = new SmtpOptions { Host = "smtp.example.com", Port = 587, Username = "u", Password = "p" }
        };

        Assert.Null(EmailOptionsGuard.Validate(options, "Production"));
    }

    [Fact]
    public void Validate_Should_Allow_Logging_Provider_Only_In_Sandbox_Environments()
    {
        var options = new EmailOptions { Provider = "Logging" };

        Assert.Null(EmailOptionsGuard.Validate(options, "Development"));
        Assert.Null(EmailOptionsGuard.Validate(options, "Staging"));
        Assert.NotNull(EmailOptionsGuard.Validate(options, "Production"));
    }

    [Fact]
    public void Validate_Should_Reject_Unknown_Provider()
    {
        var error = EmailOptionsGuard.Validate(new EmailOptions { Provider = "Carrier-Pigeon" }, "Production");

        Assert.NotNull(error);
        Assert.Contains("Email:Provider", error);
    }
}
