using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Tests.Unit;

public sealed class EmailIdentityNotificationServiceTests
{
    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static EmailIdentityNotificationService CreateService(RecordingEmailSender sender)
        => new(sender, Options.Create(new EmailOptions { AppLinkScheme = "egitimussu" }));

    [Fact]
    public async Task PasswordReset_Should_Contain_Token_And_Deep_Link()
    {
        var sender = new RecordingEmailSender();

        await CreateService(sender).SendPasswordResetAsync("ogrenci@example.com", "TOKEN123", CancellationToken.None);

        var message = Assert.Single(sender.Sent);
        Assert.Equal("ogrenci@example.com", message.ToEmail);
        Assert.Contains("TOKEN123", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("egitimussu://password-reset?token=TOKEN123", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("EğitimÜssü", message.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmailVerification_Should_Contain_Token_And_Deep_Link()
    {
        var sender = new RecordingEmailSender();

        await CreateService(sender).SendEmailVerificationAsync("veli@example.com", "ABC999", CancellationToken.None);

        var message = Assert.Single(sender.Sent);
        Assert.Equal("veli@example.com", message.ToEmail);
        Assert.Contains("egitimussu://verify-email?token=ABC999", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("ABC999", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("EğitimÜssü", message.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deep_Link_Should_Url_Encode_Base64_Token()
    {
        var sender = new RecordingEmailSender();

        // Token base64'tur: "+" ve "/" karakterleri sorgu dizesinde kodlanmazsa mobil taraf
        // "+" karakterini bosluk olarak cozer ve token gecersiz olur.
        await CreateService(sender).SendPasswordResetAsync("a@b.com", "ab+cd/ef=", CancellationToken.None);

        var message = Assert.Single(sender.Sent);
        Assert.Contains("egitimussu://password-reset?token=ab%2Bcd%2Fef%3D", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("token=ab+cd/ef=", message.HtmlBody, StringComparison.Ordinal);

        // Elle yapistirma icin ham token da bulunmali.
        Assert.Contains("ab+cd/ef=", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Templates_Should_Honour_Configured_App_Link_Scheme()
    {
        var sender = new RecordingEmailSender();
        var service = new EmailIdentityNotificationService(
            sender, Options.Create(new EmailOptions { AppLinkScheme = "ozelscheme" }));

        await service.SendPasswordResetAsync("a@b.com", "T", CancellationToken.None);

        var message = Assert.Single(sender.Sent);
        Assert.Contains("ozelscheme://password-reset?token=T", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("ozelscheme://password-reset?token=T", message.TextBody, StringComparison.Ordinal);
    }
}
