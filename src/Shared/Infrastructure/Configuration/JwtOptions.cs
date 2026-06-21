namespace EgitimUssu.Shared.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "EgitimUssu";

    public string Audience { get; set; } = "EgitimUssu.Clients";

    public string SigningKey { get; set; } = "replace-with-a-long-development-key";

    public int ExpiryMinutes { get; set; } = 60;
}
