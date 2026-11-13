namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// P02/K-01: E-posta konfigürasyonunu startup'ta doğrular (<see cref="JwtSigningKeyGuard"/> deseni:
/// <see cref="Validate"/> nedeni döner, <see cref="EnsureValid"/> fırlatır).
/// SMTP seçiliyken eksik ayarla uygulama açılmaz — "sessizce e-posta göndermeme" durumunu engeller.
/// </summary>
public static class EmailOptionsGuard
{
    /// <summary>Yapılandırma geçersizse nedenini döndürür; geçerliyse <c>null</c>.</summary>
    public static string? Validate(EmailOptions options, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.Equals(options.Provider, EmailOptions.LoggingProvider, StringComparison.OrdinalIgnoreCase))
        {
            return null;
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

        if (!isDevelopment && string.IsNullOrWhiteSpace(options.Smtp.Username))
        {
            return "Email:Smtp:Username zorunludur. Değeri Email__Smtp__Username ortam değişkeniyle verin.";
        }

        return null;
    }

    /// <summary>Yapılandırma geçersizse startup'ı fail-fast durdurur.</summary>
    public static void EnsureValid(EmailOptions options, bool isDevelopment)
    {
        var error = Validate(options, isDevelopment);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
    }
}
