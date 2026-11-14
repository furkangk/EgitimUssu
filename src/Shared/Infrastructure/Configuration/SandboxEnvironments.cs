using Microsoft.Extensions.Hosting;

namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// P15 T6 / N-24, P-01: Sahte (göndermeyen / kalıcı olmayan) sağlayıcıların hangi ortamda açılabileceğinin
/// <b>tek</b> kaynağı. Guard'lar ortam adını alır (<see cref="MigrationStartupGuard"/> deseni: saf fonksiyon,
/// <c>IHostEnvironment</c> gerektirmez).
/// <para>
/// Kural <b>fail-closed</b>: yalnız <c>Development</c> ve <c>Staging</c> izinlidir; tanınmayan her ad
/// (<c>Prod</c>, <c>Test</c>, boş) üretim sayılır — ortam adındaki bir yazım hatası şifre sıfırlama
/// kodlarını log'a düşürmesin.
/// </para>
/// </summary>
public static class SandboxEnvironments
{
    /// <summary>Ortam <c>Development</c> mı (büyük/küçük harf duyarsız).</summary>
    public static bool IsDevelopment(string? environmentName)
        => string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Logging e-posta / Local depolama gibi sahte sağlayıcılar bu ortamda açılabilir mi
    /// (yalnız <c>Development</c> ve <c>Staging</c>).
    /// </summary>
    public static bool AllowsSandboxProviders(string? environmentName)
        => IsDevelopment(environmentName)
            || string.Equals(environmentName, Environments.Staging, StringComparison.OrdinalIgnoreCase);
}
