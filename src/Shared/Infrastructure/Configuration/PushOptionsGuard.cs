namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// P03/K-02: Push konfigürasyonunu startup'ta doğrular (<see cref="EmailOptionsGuard"/> deseni:
/// <see cref="Validate"/> nedeni döner, <see cref="EnsureValid"/> fırlatır).
/// FCM seçiliyken eksik ayarla uygulama açılmaz — "sessizce push göndermeme" durumunu engeller.
/// <para>
/// P15 T6: <c>Logging</c> push <b>her ortamda</b> açılır — e-postanın aksine yük sır taşımaz ve Firebase
/// hesabı (Q8) beklenirken yayın yapılabilmeli. Development dışında <see cref="Warning"/> döner.
/// </para>
/// </summary>
public static class PushOptionsGuard
{
    /// <summary>Yapılandırma geçersizse nedenini döndürür; geçerliyse <c>null</c>.</summary>
    /// <param name="options">Push yapılandırması.</param>
    /// <param name="environmentName"><c>IHostEnvironment.EnvironmentName</c> (bugün kuralı değiştirmez; imza diğer guard'larla aynı).</param>
    public static string? Validate(PushOptions options, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (IsLogging(options))
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

    /// <summary>
    /// Açılışı durdurmayan ama log'a düşülmesi gereken durum (Development dışında Logging); yoksa <c>null</c>.
    /// Saf fonksiyondur — <c>Program.cs</c> yalnız log'a basar.
    /// </summary>
    public static string? Warning(PushOptions options, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(options);

        return IsLogging(options) && !SandboxEnvironments.IsDevelopment(environmentName)
            ? "Push:Provider = \"Logging\" — push bildirimleri GÖNDERİLMİYOR, yalnız log'a yazılıyor. Üretimde Fcm'ye geçin."
            : null;
    }

    /// <summary>Yapılandırma geçersizse startup'ı fail-fast durdurur.</summary>
    public static void EnsureValid(PushOptions options, string environmentName)
    {
        var error = Validate(options, environmentName);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
    }

    private static bool IsLogging(PushOptions options)
        => string.Equals(options.Provider, PushOptions.LoggingProvider, StringComparison.OrdinalIgnoreCase);
}
