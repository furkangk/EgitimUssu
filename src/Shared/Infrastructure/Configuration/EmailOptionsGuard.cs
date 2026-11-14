namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// P02/K-01: E-posta konfigürasyonunu startup'ta doğrular (<see cref="JwtSigningKeyGuard"/> deseni:
/// <see cref="Validate"/> nedeni döner, <see cref="EnsureValid"/> fırlatır).
/// SMTP seçiliyken eksik ayarla uygulama açılmaz — "sessizce e-posta göndermeme" durumunu engeller.
/// <para>
/// P15 T6 / N-24: <c>Logging</c> yalnız Development/Staging'de açılır (<see cref="SandboxEnvironments"/>).
/// Üretimde Logging hem e-postayı göndermez hem şifre sıfırlama kodunu log'a taşır; eskiden yalnız
/// uyarı basılıyordu, artık açılış durur.
/// </para>
/// </summary>
public static class EmailOptionsGuard
{
    /// <summary>Yapılandırma geçersizse nedenini döndürür; geçerliyse <c>null</c>.</summary>
    /// <param name="options">E-posta yapılandırması.</param>
    /// <param name="environmentName"><c>IHostEnvironment.EnvironmentName</c>.</param>
    public static string? Validate(EmailOptions options, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (IsLogging(options))
        {
            return SandboxEnvironments.AllowsSandboxProviders(environmentName)
                ? null
                : $"Email:Provider üretimde Logging olamaz (ortam: \"{environmentName}\"; Logging yalnız "
                    + "Development/Staging'de). Logging e-postayı göndermez ve şifre sıfırlama kodlarını log'a yazar. "
                    + "Email__Provider=Smtp ve Email__Smtp__* değerlerini verin.";
        }

        if (!string.Equals(options.Provider, EmailOptions.SmtpProvider, StringComparison.OrdinalIgnoreCase))
        {
            return $"Email:Provider yalnız \"{EmailOptions.LoggingProvider}\" veya \"{EmailOptions.SmtpProvider}\" olabilir.";
        }

        if (string.IsNullOrWhiteSpace(options.FromEmail))
        {
            return "Email:FromEmail zorunludur (Provider=Smtp).";
        }

        if (string.IsNullOrWhiteSpace(options.Smtp.Host))
        {
            return "Email:Smtp:Host zorunludur (Provider=Smtp).";
        }

        if (options.Smtp.Port is <= 0 or > 65535)
        {
            return "Email:Smtp:Port geçersiz (1-65535).";
        }

        if (!SandboxEnvironments.IsDevelopment(environmentName) && string.IsNullOrWhiteSpace(options.Smtp.Username))
        {
            return "Email:Smtp:Username zorunludur. Değeri Email__Smtp__Username ortam değişkeniyle verin.";
        }

        return null;
    }

    /// <summary>
    /// Açılışı durdurmayan ama log'a düşülmesi gereken durum (ör. Staging'de Logging); yoksa <c>null</c>.
    /// Saf fonksiyondur — <c>Program.cs</c> yalnız log'a basar.
    /// </summary>
    public static string? Warning(EmailOptions options, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(options);

        return IsLogging(options) && !SandboxEnvironments.IsDevelopment(environmentName)
            ? "Email:Provider = \"Logging\" — e-postalar GÖNDERİLMİYOR; log'a yalnız maskeli alıcı ve konu yazılıyor. "
                + "Üretimde Smtp gerekir."
            : null;
    }

    /// <summary>Yapılandırma geçersizse startup'ı fail-fast durdurur.</summary>
    public static void EnsureValid(EmailOptions options, string environmentName)
    {
        var error = Validate(options, environmentName);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
    }

    private static bool IsLogging(EmailOptions options)
        => string.Equals(options.Provider, EmailOptions.LoggingProvider, StringComparison.OrdinalIgnoreCase);
}
