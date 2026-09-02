using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Modules.Identity.Infrastructure;

/// <summary>
/// Kimlik akışı e-postalarını gerçekten gönderir (A-03). Önceki <c>NullIdentityNotificationService</c>
/// hiçbir şey göndermiyordu; şifre sıfırlama ve doğrulama akışları fiilen ölüydü.
/// </summary>
public sealed class EmailIdentityNotificationService(
    IEmailSender emailSender,
    IOptions<EmailOptions> options) : IIdentityNotificationService
{
    private readonly EmailOptions _options = options.Value;

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken)
        => emailSender.SendAsync(EmailTemplates.PasswordReset(email, token, _options.AppLinkScheme), cancellationToken);

    public Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken)
        => emailSender.SendAsync(EmailTemplates.EmailVerification(email, token, _options.AppLinkScheme), cancellationToken);
}
