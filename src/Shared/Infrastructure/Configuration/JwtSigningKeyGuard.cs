using System.Text;

namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// Y3: JWT imzalama anahtarını doğrular. HS256 için anahtar en az 256 bit (32 bayt) olmalı ve
/// repoya sızmış bilinen yer-tutucu değerler reddedilmelidir. Startup'ta fail-fast + health check'te kullanılır.
/// </summary>
public static class JwtSigningKeyGuard
{
    /// <summary>HS256 için asgari anahtar uzunluğu (bayt). RFC 7518, HMAC-SHA256 anahtarı ≥ hash çıktısı (256 bit).</summary>
    public const int MinimumKeyBytes = 32;

    // Repoda/örneklerde geçmişte yer almış, prod'da kesinlikle kullanılmaması gereken anahtarlar.
    private static readonly HashSet<string> KnownPlaceholders = new(StringComparer.Ordinal)
    {
        "change-this-development-signing-key",
        "replace-with-a-long-development-key"
    };

    /// <summary>Anahtar geçersizse nedenini döndürür; geçerliyse <c>null</c>.</summary>
    public static string? Validate(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return "Jwt:SigningKey ayarlanmalı (environment/secret üzerinden).";
        }

        if (KnownPlaceholders.Contains(signingKey))
        {
            return "Jwt:SigningKey bilinen bir yer-tutucu değerle bırakılamaz; güvenli bir anahtar üretin.";
        }

        if (Encoding.UTF8.GetByteCount(signingKey) < MinimumKeyBytes)
        {
            return $"Jwt:SigningKey en az {MinimumKeyBytes} bayt (256 bit) olmalı.";
        }

        return null;
    }

    /// <summary>Anahtar geçersizse startup'ı fail-fast durdurur.</summary>
    public static void EnsureValid(string? signingKey)
    {
        var error = Validate(signingKey);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
    }
}
