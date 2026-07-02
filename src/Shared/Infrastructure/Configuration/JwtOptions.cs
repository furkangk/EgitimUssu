namespace EgitimUssu.Shared.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "EgitimUssu";

    public string Audience { get; set; } = "EgitimUssu.Clients";

    // Y3: Repoda gömülü varsayılan anahtar yok. Değer environment/secret'tan gelmeli;
    // eksik/zayıf anahtar startup'ta JwtSigningKeyGuard tarafından fail-fast reddedilir.
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}
