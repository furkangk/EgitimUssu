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

    /// <summary>
    /// A-02: Uygulamanın önünde duran <b>güvenilir</b> ters-proxy sayısı. İstemci IP'si
    /// <c>X-Forwarded-For</c>'un <b>sonundan</b> bu kadar geriden okunur.
    /// <para>
    /// Neden baştan değil: başlığı istemci de gönderebilir ve proxy kendi gördüğü soket adresini
    /// listenin <i>sonuna</i> ekler. İlk girdi alınırsa istemci her istekte uydurma bir değer
    /// göndererek kendine taze bir kova açar ve hız sınırı tümüyle işlevsiz kalır.
    /// </para>
    /// <para>
    /// <c>0</c> → başlık hiç dikkate alınmaz, soket adresi kullanılır (proxy'siz/doğrudan yayın).
    /// Render tek proxy arkasındadır: <c>1</c>.
    /// </para>
    /// </summary>
    public int TrustedProxyHopCount { get; set; } = 1;

    /// <summary>
    /// Kimlik bilgisi alan uçlar (giriş, kayıt, şifre sıfırlama, e-posta doğrulama): IP başına sıkı.
    /// Hesap boyutu <c>ILoginAttemptThrottle</c>'dadır (hesap + IP).
    /// </summary>
    public RateLimitRule Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>
    /// P15 T7 / N-32: <c>/api/identity/refresh</c>. Uç anonimdir (erişim token'ı süresi dolmuştur,
    /// mobil bearer göndermez) — kullanıcı başına sayılamaz, IP başınadır ama <see cref="Auth"/>'tan
    /// gevşektir: okul NAT'ı arkasında ders başında herkes bir kez yeniler. 48 baytlık rastgele token
    /// kaba kuvvete açık değildir; kova yalnız taşkına karşıdır.
    /// </summary>
    public RateLimitRule Refresh { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    /// <summary>
    /// P15 T7 / N-32: Oturum açmış kullanıcının kimlik uçları (<c>/me</c>, <c>/sessions</c>,
    /// <c>/logout</c>, <c>/account/*</c>, <c>/users/*</c>) — <b>kullanıcı</b> başına (oturum yoksa IP).
    /// IP başına 10/dk iken aynı ağdaki bir sınıf birbirinin kotasını tüketiyordu.
    /// </summary>
    public RateLimitRule Session { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

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
