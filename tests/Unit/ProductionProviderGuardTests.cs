using EgitimUssu.Shared.Infrastructure.Configuration;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P15 Task 6 / N-24, P-01: Sahte sağlayıcılar (<c>Email:Provider=Logging</c>,
/// <c>Storage:Provider=Local</c>) yalnız <b>Development ve Staging</b>'de açılır.
/// <para>
/// Üretimde Logging e-posta göndermez, şifre sıfırlama kodlarını log'a düşürür (log'a erişen herkes
/// istediği hesabın şifresini sıfırlayabilir); Local depolama Render'ın geçici diskine yazar (her
/// deploy'da yüklenen dosyalar kaybolur). İkisi de <b>fail-fast</b> reddedilir. Kural fail-closed:
/// tanınmayan ortam adı (<c>Prod</c>, <c>Test</c>…) da üretim sayılır — yazım hatası sızıntı açmasın.
/// </para>
/// <para>
/// Push'ta Logging üretimde <b>izinlidir</b> ama uyarı üretir: push yükü sır taşımaz ve Firebase
/// hesabı (Q8) beklenirken yayın yapılabilmeli. Uyarı saf fonksiyondan döner, <c>Program.cs</c>
/// yalnız log'a basar.
/// </para>
/// </summary>
public sealed class ProductionProviderGuardTests
{
    private static EmailOptions CompleteSmtp() => new()
    {
        Provider = EmailOptions.SmtpProvider,
        FromEmail = "no-reply@egitimussu.com",
        Smtp = new SmtpOptions { Host = "smtp.example.com", Port = 587, Username = "u", Password = "p" }
    };

    private static StorageOptions CompleteS3() => new()
    {
        Provider = StorageOptions.S3Provider,
        BucketName = "egitimussu",
        AccessKeyId = "key",
        SecretAccessKey = "secret"
    };

    // --- E-posta ----------------------------------------------------------------------------

    [Fact]
    public void Email_Logging_Should_Be_Rejected_In_Production()
    {
        var error = EmailOptionsGuard.Validate(new EmailOptions { Provider = "Logging" }, "Production");

        Assert.NotNull(error);
        Assert.Contains("Email:Provider üretimde Logging olamaz", error);
    }

    [Fact]
    public void Email_Logging_Should_Fail_Fast_In_Production()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => EmailOptionsGuard.EnsureValid(new EmailOptions(), "Production"));

        Assert.Contains("Email:Provider üretimde Logging olamaz", exception.Message);
    }

    [Theory]
    [InlineData("Prod")]
    [InlineData("Test")]
    [InlineData("production")]
    [InlineData("")]
    public void Email_Logging_Should_Be_Rejected_In_Any_Environment_Other_Than_Development_Or_Staging(
        string environmentName)
        => Assert.NotNull(EmailOptionsGuard.Validate(new EmailOptions { Provider = "logging" }, environmentName));

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("staging")]
    [InlineData("DEVELOPMENT")]
    public void Email_Logging_Should_Be_Allowed_In_Development_And_Staging(string environmentName)
        => Assert.Null(EmailOptionsGuard.Validate(new EmailOptions { Provider = "Logging" }, environmentName));

    [Fact]
    public void Email_Complete_Smtp_Should_Pass_In_Production()
        => Assert.Null(EmailOptionsGuard.Validate(CompleteSmtp(), "Production"));

    [Fact]
    public void Email_Logging_In_Staging_Should_Warn()
    {
        var warning = EmailOptionsGuard.Warning(new EmailOptions { Provider = "Logging" }, "Staging");

        Assert.NotNull(warning);
        Assert.Contains("Email:Provider", warning);
    }

    [Theory]
    [InlineData("Development", "Logging")]
    [InlineData("Production", "Smtp")]
    [InlineData("Staging", "Smtp")]
    public void Email_Should_Not_Warn_In_Development_Or_With_Smtp(string environmentName, string provider)
    {
        var options = CompleteSmtp();
        options.Provider = provider;

        Assert.Null(EmailOptionsGuard.Warning(options, environmentName));
    }

    // --- Depolama ---------------------------------------------------------------------------

    [Fact]
    public void Storage_Local_Should_Be_Rejected_In_Production()
    {
        var error = StorageOptionsGuard.Validate(new StorageOptions(), "Production");

        Assert.NotNull(error);
        Assert.Contains("Storage:Provider üretimde Local olamaz", error);
    }

    [Fact]
    public void Storage_Local_Should_Fail_Fast_In_Production()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => StorageOptionsGuard.EnsureValid(new StorageOptions { Provider = "local" }, "Production"));

        Assert.Contains("Storage:Provider üretimde Local olamaz", exception.Message);
    }

    [Theory]
    [InlineData("Prod")]
    [InlineData("Test")]
    [InlineData("")]
    public void Storage_Local_Should_Be_Rejected_In_Any_Environment_Other_Than_Development_Or_Staging(
        string environmentName)
        => Assert.NotNull(StorageOptionsGuard.Validate(new StorageOptions(), environmentName));

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void Storage_Local_Should_Be_Allowed_In_Development_And_Staging(string environmentName)
        => Assert.Null(StorageOptionsGuard.Validate(new StorageOptions(), environmentName));

    [Fact]
    public void Storage_Complete_S3_Should_Pass_In_Production()
        => Assert.Null(StorageOptionsGuard.Validate(CompleteS3(), "Production"));

    // --- Push -------------------------------------------------------------------------------

    [Fact]
    public void Push_Logging_Should_Pass_With_Warning_In_Production()
    {
        var options = new PushOptions { Provider = "Logging" };

        Assert.Null(PushOptionsGuard.Validate(options, "Production"));
        PushOptionsGuard.EnsureValid(options, "Production");

        var warning = PushOptionsGuard.Warning(options, "Production");
        Assert.NotNull(warning);
        Assert.Contains("Push:Provider", warning);
    }

    [Fact]
    public void Push_Logging_Should_Not_Warn_In_Development()
        => Assert.Null(PushOptionsGuard.Warning(new PushOptions(), "Development"));

    [Fact]
    public void Push_Fcm_Should_Not_Warn_In_Production()
    {
        var options = new PushOptions
        {
            Provider = PushOptions.FcmProvider,
            ProjectId = "egitimussu",
            ServiceAccountJson = "{\"type\":\"service_account\"}"
        };

        Assert.Null(PushOptionsGuard.Validate(options, "Production"));
        Assert.Null(PushOptionsGuard.Warning(options, "Production"));
    }
}
