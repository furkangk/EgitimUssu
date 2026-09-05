using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Modules.Identity.Infrastructure;

/// <summary>
/// Kimlik akışı e-postalarını <b>kuyruğa alır</b> (A-03/A-04).
/// <para>
/// Önceki hâli SMTP'ye doğrudan, HTTP isteğinin içinden gidiyordu ve hatayı yeniden fırlatıyordu:
/// sağlayıcı yavaş ya da kapalıysa kullanıcı oluşmuş olmasına rağmen istek 500 dönüyor, kullanıcı
/// tekrar denediğinde <c>duplicate_email</c> alıyordu. Artık kayıt, iş yazımıyla aynı transaction'da
/// kuyruğa girer; gönderimi <see cref="OutgoingEmailDispatcher"/> retry/backoff/dead-letter ile üstlenir.
/// </para>
/// <para>
/// <b>SaveChanges burada çağrılmaz</b> — çağıran handler kendi yazımıyla birlikte kaydeder,
/// böylece "kullanıcı yazıldı ama e-posta kuyruğa girmedi" durumu oluşamaz.
/// </para>
/// </summary>
public sealed class EmailIdentityNotificationService(
    IEmailQueue emailQueue,
    IdentityDbContext dbContext,
    IOptions<EmailOptions> options) : IIdentityNotificationService
{
    private readonly EmailOptions _options = options.Value;

    /// <inheritdoc />
    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken)
    {
        emailQueue.Enqueue(dbContext, EmailTemplates.PasswordReset(email, token, _options.AppLinkScheme));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken)
    {
        emailQueue.Enqueue(dbContext, EmailTemplates.EmailVerification(email, token, _options.AppLinkScheme));
        return Task.CompletedTask;
    }
}
