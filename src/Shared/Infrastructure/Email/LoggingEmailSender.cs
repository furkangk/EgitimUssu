using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Email;

/// <summary>
/// E-postayı göndermez, log'a basar. Geliştirme ve testte varsayılan (<c>Email:Provider = "Logging"</c>).
/// Üretimde seçilirse startup'ta uyarı basılır (bkz. <c>Program.cs</c>) — sessiz kayıp olmasın.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "E-POSTA (gönderilmedi, Provider=Logging) → {To} | Konu: {Subject}\n{Body}",
            message.ToEmail,
            message.Subject,
            message.TextBody);

        return Task.CompletedTask;
    }
}
