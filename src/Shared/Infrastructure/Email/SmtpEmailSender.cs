using EgitimUssu.Shared.Infrastructure.Configuration;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EgitimUssu.Shared.Infrastructure.Email;

/// <summary>SMTP istemcisi üretir; testte sahte transport enjekte edilebilsin diye ayrıldı.</summary>
public interface ISmtpClientFactory
{
    IMailTransport Create();
}

/// <summary>Üretimde gerçek MailKit <see cref="SmtpClient"/> döndürür.</summary>
public sealed class MailKitSmtpClientFactory : ISmtpClientFactory
{
    public IMailTransport Create() => new SmtpClient();
}

/// <summary>
/// Herhangi bir SMTP relay'i (Gmail, Amazon SES, SendGrid, Resend …) üzerinden e-posta gönderir.
/// Sağlayıcı değişimi yalnız konfigürasyon değişikliğidir (karar K-01).
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger,
    ISmtpClientFactory clientFactory) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = clientFactory.Create();

        try
        {
            await client.ConnectAsync(
                _options.Smtp.Host,
                _options.Smtp.Port,
                _options.Smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
            {
                await client.AuthenticateAsync(_options.Smtp.Username, _options.Smtp.Password, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception exception)
        {
            // Sessiz yutma yok: hata log'lanır ve çağırana yükseltilir (master tasarım §1 "Sessiz başarısızlık yok").
            logger.LogError(exception, "E-posta gönderilemedi: {To} / {Subject}", message.ToEmail, message.Subject);
            throw;
        }
    }
}
