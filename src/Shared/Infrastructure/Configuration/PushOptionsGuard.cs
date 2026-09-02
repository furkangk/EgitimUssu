namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// P03/K-02: Push konfigürasyonunu startup'ta doğrular (<see cref="EmailOptionsGuard"/> deseni:
/// <see cref="Validate"/> nedeni döner, <see cref="EnsureValid"/> fırlatır).
/// FCM seçiliyken eksik ayarla uygulama açılmaz — "sessizce push göndermeme" durumunu engeller.
/// </summary>
public static class PushOptionsGuard
{
    /// <summary>Yapılandırma geçersizse nedenini döndürür; geçerliyse <c>null</c>.</summary>
    public static string? Validate(PushOptions options, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.Equals(options.Provider, PushOptions.LoggingProvider, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.Equals(options.Provider, PushOptions.FcmProvider, StringComparison.OrdinalIgnoreCase))
        {
            return $"Push:Provider yalnız \"{PushOptions.LoggingProvider}\" veya \"{PushOptions.FcmProvider}\" olabilir.";
        }

        if (string.IsNullOrWhiteSpace(options.ProjectId))
        {
            return "Push:ProjectId zorunludur (Provider=Fcm).";
        }

        if (string.IsNullOrWhiteSpace(options.ServiceAccountJson)
            && string.IsNullOrWhiteSpace(options.ServiceAccountJsonPath))
        {
            return "Push:ServiceAccountJson veya Push:ServiceAccountJsonPath zorunludur (Provider=Fcm). "
                + "Değeri Push__ServiceAccountJson ortam değişkeniyle verin.";
        }

        return null;
    }

    /// <summary>Yapılandırma geçersizse startup'ı fail-fast durdurur.</summary>
    public static void EnsureValid(PushOptions options, bool isDevelopment)
    {
        var error = Validate(options, isDevelopment);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
    }
}
