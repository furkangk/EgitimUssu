using EgitimUssu.Shared.Infrastructure.Configuration;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P03/K-02: Push konfigürasyonu startup'ta doğrulanır. FCM seçiliyken eksik ayarla uygulama açılmaz
/// (sessizce push göndermeme yasak); "Logging" sağlayıcısı her ortamda açılır — üretimde uyarıyla
/// (P15 T6: yük sır taşımaz, Firebase Q8 beklenirken yayın yapılabilsin; bkz. <see cref="ProductionProviderGuardTests"/>).
/// Guard, <see cref="EmailOptionsGuard"/> desenini izler: <c>Validate</c> nedeni döner, <c>EnsureValid</c> fırlatır.
/// </summary>
public sealed class PushOptionsGuardTests
{
    [Fact]
    public void EnsureValid_Should_Throw_When_Fcm_ProjectId_Missing()
    {
        var options = new PushOptions { Provider = PushOptions.FcmProvider };

        Assert.Throws<InvalidOperationException>(() => PushOptionsGuard.EnsureValid(options, "Production"));
    }

    [Fact]
    public void EnsureValid_Should_Throw_When_Fcm_Credentials_Missing()
    {
        var options = new PushOptions { Provider = PushOptions.FcmProvider, ProjectId = "egitimussu" };

        Assert.Throws<InvalidOperationException>(() => PushOptionsGuard.EnsureValid(options, "Production"));
    }

    [Fact]
    public void EnsureValid_Should_Allow_Fcm_With_Inline_Json()
    {
        var options = new PushOptions
        {
            Provider = PushOptions.FcmProvider,
            ProjectId = "egitimussu",
            ServiceAccountJson = "{\"type\":\"service_account\"}"
        };

        PushOptionsGuard.EnsureValid(options, "Production");
    }

    [Fact]
    public void EnsureValid_Should_Allow_Fcm_With_Json_Path()
    {
        var options = new PushOptions
        {
            Provider = PushOptions.FcmProvider,
            ProjectId = "egitimussu",
            ServiceAccountJsonPath = "/secrets/fcm.json"
        };

        PushOptionsGuard.EnsureValid(options, "Production");
    }

    [Fact]
    public void EnsureValid_Should_Reject_Unknown_Provider()
    {
        var options = new PushOptions { Provider = "OneSignal" };

        Assert.Throws<InvalidOperationException>(() => PushOptionsGuard.EnsureValid(options, "Production"));
    }

    [Fact]
    public void Validate_Should_Pass_For_Logging_Provider_In_Production()
        => Assert.Null(PushOptionsGuard.Validate(new PushOptions(), "Production"));
}
