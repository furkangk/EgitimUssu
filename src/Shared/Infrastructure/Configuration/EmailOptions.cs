namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// K-01: Sağlayıcı-agnostik e-posta yapılandırması. Sırlar (kullanıcı adı/parola) repoda tutulmaz;
/// yalnız <c>Email__Smtp__Username</c> / <c>Email__Smtp__Password</c> ortam değişkenleriyle verilir.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Geliştirme/testte log'a basan sağlayıcı.</summary>
    public const string LoggingProvider = "Logging";

    /// <summary>Üretimde herhangi bir SMTP relay (Gmail/SES/SendGrid/Resend).</summary>
    public const string SmtpProvider = "Smtp";

    /// <summary>"Logging" (dev/test) veya "Smtp".</summary>
    public string Provider { get; set; } = LoggingProvider;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "EğitimÜssü";

    /// <summary>Mobil derin bağlantı şeması: e-postadaki bağlantı "egitimussu://..." ile başlar.</summary>
    public string AppLinkScheme { get; set; } = "egitimussu";

    public SmtpOptions Smtp { get; set; } = new();
}

/// <summary>SMTP relay bağlantı ayarları. <see cref="EmailOptions.Provider"/> "Smtp" iken kullanılır.</summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
