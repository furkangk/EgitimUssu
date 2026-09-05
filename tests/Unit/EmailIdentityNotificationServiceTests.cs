using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Email;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// Kimlik e-postalarının içeriği ve derin bağlantıları.
/// <para>
/// A-04: Servis artık SMTP'ye <b>göndermiyor</b>, kuyruğa alıyor — bu yüzden test de göndericiyi
/// değil kuyruğu dinler. Gönderim sözleşmesi değişti, şablon sözleşmesi değişmedi.
/// </para>
/// </summary>
public sealed class EmailIdentityNotificationServiceTests
{
    private sealed class RecordingEmailQueue : IEmailQueue
    {
        public List<EmailMessage> Queued { get; } = [];

        public void Enqueue(ModuleDbContext dbContext, EmailMessage message)
        {
            Assert.NotNull(dbContext); // kuyruk her zaman bir modül context'ine yazar
            Queued.Add(message);
        }
    }

    private static IdentityDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-email-{Guid.NewGuid():N}")
            .Options;
        return new IdentityDbContext(options, new NoOpDomainEventMapper());
    }

    private static EmailIdentityNotificationService CreateService(
        RecordingEmailQueue queue,
        string appLinkScheme = "egitimussu")
        => new(queue, NewDbContext(), Options.Create(new EmailOptions { AppLinkScheme = appLinkScheme }));

    [Fact]
    public async Task PasswordReset_Should_Contain_Token_And_Deep_Link()
    {
        var queue = new RecordingEmailQueue();

        await CreateService(queue).SendPasswordResetAsync("ogrenci@example.com", "TOKEN123", CancellationToken.None);

        var message = Assert.Single(queue.Queued);
        Assert.Equal("ogrenci@example.com", message.ToEmail);
        Assert.Contains("TOKEN123", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("egitimussu://password-reset?token=TOKEN123", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("EğitimÜssü", message.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmailVerification_Should_Contain_Token_And_Deep_Link()
    {
        var queue = new RecordingEmailQueue();

        await CreateService(queue).SendEmailVerificationAsync("veli@example.com", "ABC999", CancellationToken.None);

        var message = Assert.Single(queue.Queued);
        Assert.Equal("veli@example.com", message.ToEmail);
        Assert.Contains("egitimussu://verify-email?token=ABC999", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("ABC999", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("EğitimÜssü", message.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deep_Link_Should_Url_Encode_Base64_Token()
    {
        var queue = new RecordingEmailQueue();

        // Token base64'tur: "+" ve "/" karakterleri sorgu dizesinde kodlanmazsa mobil taraf
        // "+" karakterini bosluk olarak cozer ve token gecersiz olur.
        await CreateService(queue).SendPasswordResetAsync("a@b.com", "ab+cd/ef=", CancellationToken.None);

        var message = Assert.Single(queue.Queued);
        Assert.Contains("egitimussu://password-reset?token=ab%2Bcd%2Fef%3D", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("token=ab+cd/ef=", message.HtmlBody, StringComparison.Ordinal);

        // Elle yapistirma icin ham token da bulunmali.
        Assert.Contains("ab+cd/ef=", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Templates_Should_Honour_Configured_App_Link_Scheme()
    {
        var queue = new RecordingEmailQueue();

        await CreateService(queue, appLinkScheme: "ozelscheme")
            .SendPasswordResetAsync("a@b.com", "T", CancellationToken.None);

        var message = Assert.Single(queue.Queued);
        Assert.Contains("ozelscheme://password-reset?token=T", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("ozelscheme://password-reset?token=T", message.TextBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// A-04 sözleşmesi: kuyruğa alma <b>SaveChanges çağırmaz</b>. Çağıran handler kendi iş
    /// yazımıyla birlikte kaydeder; aksi hâlde "kullanıcı yazıldı ama e-posta kuyruğa girmedi"
    /// (ya da tersi) durumu oluşabilirdi.
    /// </summary>
    [Fact]
    public async Task Enqueue_Should_Not_Save_Changes()
    {
        var dbContext = NewDbContext();
        var service = new EmailIdentityNotificationService(
            new EmailQueue(new TestIdGenerator(), new TestClock()),
            dbContext,
            Options.Create(new EmailOptions { AppLinkScheme = "egitimussu" }));

        await service.SendEmailVerificationAsync("a@b.com", "T", CancellationToken.None);

        Assert.Single(dbContext.ChangeTracker.Entries<OutgoingEmail>());
        Assert.Empty(await dbContext.OutgoingEmails.AsNoTracking().ToListAsync());
    }

    private sealed class TestIdGenerator : EgitimUssu.Shared.Kernel.IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class TestClock : EgitimUssu.Shared.Kernel.IClock
    {
        public DateTime UtcNow => new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
    }
}
