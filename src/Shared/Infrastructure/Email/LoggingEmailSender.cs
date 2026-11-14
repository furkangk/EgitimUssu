using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Email;

/// <summary>
/// E-postayı göndermez, log'a basar (<c>Email:Provider = "Logging"</c>). Yalnız Development ve Staging'de
/// açılır — üretimde <see cref="EmailOptionsGuard"/> açılışı durdurur (P15 T6 / N-24).
/// <para>
/// Gövde şifre sıfırlama / doğrulama kodunu taşır; log'a düşen tek bir kod, log'a erişen herkese o hesabı
/// teslim eder. Bu yüzden gövde <b>hiçbir ortamda</b> yazılmaz: log satırı maskeli alıcı
/// (<see cref="SensitiveDataMask.MaskEmail"/>) + konu + gövde uzunluğu taşır. Tek istisna geliştiricinin
/// kendi makinesidir: Development <b>ve</b> <c>Email:Logging:IncludeBodyInDevelopment=true</c>. Bayrak
/// Staging/Production'da yok sayılır.
/// </para>
/// </summary>
public sealed class LoggingEmailSender(
    ILogger<LoggingEmailSender> logger,
    IOptions<EmailOptions> options,
    IHostEnvironment environment) : IEmailSender
{
    private readonly bool _includeBody = options.Value.Logging.IncludeBodyInDevelopment
        && SandboxEnvironments.IsDevelopment(environment.EnvironmentName);

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var recipient = SensitiveDataMask.MaskEmail(message.ToEmail);

        if (_includeBody)
        {
            logger.LogInformation(
                "E-POSTA (gönderilmedi, Provider=Logging) → {To} | Konu: {Subject}\n{Body}",
                recipient,
                message.Subject,
                message.TextBody);
        }
        else
        {
            logger.LogInformation(
                "E-POSTA (gönderilmedi, Provider=Logging) → {To} | Konu: {Subject} | Gövde log'a yazılmadı ({BodyLength} karakter)",
                recipient,
                message.Subject,
                message.TextBody.Length);
        }

        return Task.CompletedTask;
    }
}
