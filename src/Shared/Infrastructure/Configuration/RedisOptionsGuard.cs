namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// A-01: Redis yapılandırmasını startup'ta doğrular (<see cref="ConnectionStringGuard"/> deseni:
/// <see cref="Validate"/> nedeni döner, <see cref="EnsureValid"/> fırlatır).
/// <para>
/// Redis'e bağlı <b>dört</b> koruma fail-open çalışır: dağıtık hız sınırı, token kara listesi
/// (logout'ta anlık iptal), giriş denemesi kilidi ve <c>Idempotency-Key</c> tekrarı. Erişilemeyen
/// Redis bu dördünü birden <b>sessizce</b> kapatır — istisna fırlatmaz, istek başarılı görünür.
/// Bu yüzden yapılandırmanın <i>varlığı</i> diğer sırlar gibi startup'ta zorlanır: üretimde
/// eksik ya da <c>localhost</c> adresi (ortam değişkeninin unutulduğunun imzası) kabul edilmez.
/// </para>
/// </summary>
public static class RedisOptionsGuard
{
    // Üretimde bir uygulama kendi konteynerindeki Redis'e bağlanmaz; bu adresler yapılandırmanın
    // hiç verilmediğini (varsayılanın kaldığını) gösterir.
    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "::1", "[::1]"];

    /// <summary>Yapılandırma geçersizse nedenini döndürür; geçerliyse <c>null</c>.</summary>
    /// <param name="options">Redis ayarları.</param>
    /// <param name="isDevelopment">Geliştirme ortamı mı (yerel Redis ve Redis'siz çalışma serbesttir).</param>
    /// <returns>Hata nedeni ya da <c>null</c>.</returns>
    public static string? Validate(RedisOptions options, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Geliştirmede Redis'in hiç ayakta olmaması normaldir: fail-open davranışı zaten
        // istenen deneyimdir, uygulamayı açtırmamak geliştiriciyi engellemekten başka işe yaramaz.
        if (isDevelopment)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.Configuration))
        {
            return "Redis:Configuration boş. Adresi Redis__Configuration ortam değişkeniyle verin — "
                + "eksikken hız sınırı, token kara listesi, giriş kilidi ve idempotency sessizce devre dışı kalır.";
        }

        if (IsLoopback(options.Configuration))
        {
            return "Redis:Configuration loopback adresi gösteriyor (varsayılan değer kalmış). "
                + "Üretimde gerçek Redis adresini Redis__Configuration ile verin.";
        }

        return null;
    }

    /// <summary>Yapılandırma geçersizse startup'ı fail-fast durdurur.</summary>
    /// <param name="options">Redis ayarları.</param>
    /// <param name="isDevelopment">Geliştirme ortamı mı.</param>
    public static void EnsureValid(RedisOptions options, bool isDevelopment)
    {
        var error = Validate(options, isDevelopment);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
    }

    /// <summary>Bağlantı dizesindeki ilk uç noktanın host'u loopback mi.</summary>
    private static bool IsLoopback(string configuration)
    {
        // StackExchange.Redis dizesi "host:port,host2:port,option=value" biçimindedir.
        var firstEndpoint = configuration.Split(',', StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        // Şemalı (redis://user:pass@host:port) biçimi de kabul edilir.
        var schemeIndex = firstEndpoint.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            firstEndpoint = firstEndpoint[(schemeIndex + 3)..];
        }

        var atIndex = firstEndpoint.LastIndexOf('@');
        if (atIndex >= 0)
        {
            firstEndpoint = firstEndpoint[(atIndex + 1)..];
        }

        // IPv6 köşeli parantez biçimini bozmadan port ayır.
        var host = firstEndpoint.StartsWith('[')
            ? firstEndpoint[..(firstEndpoint.IndexOf(']', StringComparison.Ordinal) is var end && end > 0 ? end + 1 : firstEndpoint.Length)]
            : firstEndpoint.Split(':', 2)[0];

        return LoopbackHosts.Contains(host.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
