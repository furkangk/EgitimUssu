namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// P04/K-03: Depolama konfigürasyonunu startup'ta doğrular (<see cref="EmailOptionsGuard"/> deseni:
/// <see cref="Validate"/> nedeni döner, <see cref="EnsureValid"/> fırlatır).
/// S3 seçiliyken eksik ayarla uygulama açılmaz — dosyanın "yüklendi görünüp kaybolması" engellenir.
/// <para>
/// P15 T6 / P-01: <c>Local</c> yalnız Development/Staging'de açılır (<see cref="SandboxEnvironments"/>).
/// Üretimde Local, konteynerin geçici diskine yazar: her deploy'da yüklenen dosyalar kaybolur.
/// </para>
/// </summary>
public static class StorageOptionsGuard
{
    /// <summary>Yapılandırma geçersizse nedenini döndürür; geçerliyse <c>null</c>.</summary>
    /// <param name="options">Depolama yapılandırması.</param>
    /// <param name="environmentName"><c>IHostEnvironment.EnvironmentName</c>.</param>
    public static string? Validate(StorageOptions options, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.Equals(options.Provider, StorageOptions.LocalProvider, StringComparison.OrdinalIgnoreCase))
        {
            if (!SandboxEnvironments.AllowsSandboxProviders(environmentName))
            {
                return $"Storage:Provider üretimde Local olamaz (ortam: \"{environmentName}\"; Local yalnız "
                    + "Development/Staging'de). Local konteynerin geçici diskine yazar, yüklenen dosyalar her deploy'da kaybolur. "
                    + "Storage__Provider=S3 ve Storage__BucketName/AccessKeyId/SecretAccessKey değerlerini verin.";
            }

            return string.IsNullOrWhiteSpace(options.LocalRootPath)
                ? "Storage:LocalRootPath zorunludur (Provider=Local)."
                : null;
        }

        if (!string.Equals(options.Provider, StorageOptions.S3Provider, StringComparison.OrdinalIgnoreCase))
        {
            return $"Storage:Provider yalniz \"{StorageOptions.LocalProvider}\" veya \"{StorageOptions.S3Provider}\" olabilir.";
        }

        if (string.IsNullOrWhiteSpace(options.BucketName))
        {
            return "Storage:BucketName zorunludur (Provider=S3).";
        }

        if (string.IsNullOrWhiteSpace(options.AccessKeyId))
        {
            return "Storage:AccessKeyId zorunludur. Degeri Storage__AccessKeyId ortam degiskeniyle verin.";
        }

        if (string.IsNullOrWhiteSpace(options.SecretAccessKey))
        {
            return "Storage:SecretAccessKey zorunludur. Degeri Storage__SecretAccessKey ortam degiskeniyle verin.";
        }

        return null;
    }

    /// <summary>Yapılandırma geçersizse startup'ı fail-fast durdurur.</summary>
    public static void EnsureValid(StorageOptions options, string environmentName)
    {
        var error = Validate(options, environmentName);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
    }
}
