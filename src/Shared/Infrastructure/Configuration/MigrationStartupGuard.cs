using Microsoft.Extensions.Hosting;

namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// E-05: "Açılışta migration uygulanmalı mı?" kararının <b>tek</b> sahibi.
/// <para>
/// Üretimde <b>hiçbir koşulda</b> uygulanmaz — <c>Database:ApplyMigrationsOnStartup</c> yanlışlıkla
/// açılsa bile. Gerekçe iki yönlü: (1) şema değişikliği geri alınması en pahalı işlemdir ve
/// uygulamanın açılış anına bağlanırsa <i>ne zaman</i> uygulandığı deploy ile aynı saniyeye
/// gömülür — sorun çıktığında hangi adımın kırdığı ayırt edilemez; (2) birden çok örnek aynı anda
/// açıldığında migration'lar yarışır. Üretimde bu, gözetilen ayrı bir deploy adımıdır.
/// </para>
/// <para>
/// Saf bir fonksiyondur: host ayağa kaldırmadan, tablo halinde test edilir
/// (<c>MigrationStartupGuardTests</c>). Aynı desen <c>DemoDataSeedGuard</c>'da da kullanılır.
/// </para>
/// </summary>
public static class MigrationStartupGuard
{
    /// <summary>Açılışta migration engelliyse nedenini döndürür; serbestse <c>null</c>.</summary>
    /// <param name="environmentName">Çalışma ortamının adı (<c>ASPNETCORE_ENVIRONMENT</c>).</param>
    /// <param name="enabled"><c>Database:ApplyMigrationsOnStartup</c> bayrağı.</param>
    public static string? BlockReason(string environmentName, bool enabled)
    {
        if (string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase))
        {
            return "Ortam Production — migration açılışta uygulanmaz. Şema değişikliği ayrı bir "
                + "deploy adımıdır: 'dotnet ef database update' (bkz. doc/architecture/backup_restore.md).";
        }

        return enabled ? null : "Database:ApplyMigrationsOnStartup = false.";
    }
}
