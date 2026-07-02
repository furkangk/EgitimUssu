namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// Y4: Dağıtık rate limiting ayarları. Politika, istek yoluna göre seçilir:
/// <c>/api/identity/*</c> → <see cref="Auth"/> (sıkı, brute-force'a karşı), diğer <c>/api/*</c> → <see cref="Default"/>.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public RateLimitRule Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    public RateLimitRule Default { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };
}

public sealed class RateLimitRule
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}
