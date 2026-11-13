namespace EgitimUssu.Shared.Infrastructure.Email;

/// <summary>
/// K-01: E-posta gönderimi. Uygulama katmanları yalnız bu arayüzü görür;
/// sağlayıcıya özel tip (SMTP/MailKit vb.) Infrastructure'da kalır.
/// </summary>
public interface IEmailSender
{
    /// <summary>E-postayı gönderir. Sağlayıcı hatası istisna olarak yükselir; sessizce yutulmaz.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
