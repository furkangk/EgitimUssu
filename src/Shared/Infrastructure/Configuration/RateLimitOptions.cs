namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// Y4: Dağıtık rate limiting ayarları. Politika, istek yoluna göre seçilir:
/// <c>/api/identity/*</c> → <see cref="Auth"/> (sıkı, brute-force'a karşı),
/// <c>POST /api/messaging/conversations/*/messages</c> → <see cref="Messaging"/> (spam'e karşı, <b>kullanıcı</b> başına),
/// diğer <c>/api/*</c> → <see cref="Default"/>.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public RateLimitRule Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    public RateLimitRule Default { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    /// <summary>
    /// M16-1: Mesaj gönderimi. Diğer politikalardan farklı olarak partition <b>kullanıcı</b> kimliğidir:
    /// IP'ye göre sayılsaydı aynı okul/ev ağındaki kullanıcılar birbirinin kotasını tüketirdi.
    /// </summary>
    public RateLimitRule Messaging { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };
}

public sealed class RateLimitRule
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}
