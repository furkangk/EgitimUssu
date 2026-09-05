using System.Text;

namespace EgitimUssu.Shared.Infrastructure.Observability;

/// <summary>
/// C-04: Log ve trace alanlarına giden metinlerden PII/sırları temizler.
/// <para>
/// Log'lar toplayıcıya gider ve orada uzun süre saklanır; oraya düşen tek bir doğrulama token'ı
/// ya da e-posta adresi, kalıcı bir güvenlik ve KVKK sorunudur. Bu yüzden maskeleme
/// <b>izin listesi değil yasak listesiyle</b> çalışır: adı hassas bir anahtarı <i>içeren</i> her
/// sorgu parametresi maskelenir (<c>accessToken</c>, <c>refresh_token</c>, <c>userEmail</c> …).
/// </para>
/// </summary>
public static class SensitiveDataMask
{
    /// <summary>Maskelenmiş değerin yerine yazılan işaret.</summary>
    public const string Placeholder = "***";

    /// <summary>Adında bunlardan biri geçen her sorgu parametresi maskelenir.</summary>
    private static readonly string[] SensitiveKeys =
    [
        "token", "password", "secret", "email", "phone", "apikey", "authorization",
    ];

    /// <summary>
    /// Yol + sorgu dizesindeki hassas parametre <b>değerlerini</b> maskeler; anahtarlar ve
    /// hassas olmayan değerler korunur (log satırı okunabilir kalmalı, yoksa kimse bakmaz).
    /// </summary>
    /// <param name="pathAndQuery">Ör. <c>/api/x?page=2&amp;token=abc</c>.</param>
    /// <returns>Maskelenmiş metin.</returns>
    public static string MaskQueryString(string? pathAndQuery)
    {
        if (string.IsNullOrEmpty(pathAndQuery))
        {
            return string.Empty;
        }

        var separator = pathAndQuery.IndexOf('?');
        if (separator < 0 || separator == pathAndQuery.Length - 1)
        {
            return pathAndQuery;
        }

        var path = pathAndQuery[..(separator + 1)];
        var query = pathAndQuery[(separator + 1)..];
        var builder = new StringBuilder(path.Length + query.Length);
        builder.Append(path);

        var first = true;
        foreach (var pair in query.Split('&'))
        {
            if (!first)
            {
                builder.Append('&');
            }

            first = false;
            builder.Append(MaskPair(pair));
        }

        return builder.ToString();
    }

    /// <summary>Bir parametre adının hassas sayılıp sayılmadığı.</summary>
    /// <param name="key">Parametre adı.</param>
    /// <returns>Hassassa <c>true</c>.</returns>
    public static bool IsSensitiveKey(string key) => SensitiveKeys.Any(
        sensitive => key.Contains(sensitive, StringComparison.OrdinalIgnoreCase));

    private static string MaskPair(string pair)
    {
        var equals = pair.IndexOf('=');
        if (equals < 0)
        {
            // Değeri olmayan parametre (ör. `?flag`): maskelenecek bir şey yok, olduğu gibi kalır.
            return pair;
        }

        var key = pair[..equals];
        return IsSensitiveKey(key) ? $"{key}={Placeholder}" : pair;
    }
}
