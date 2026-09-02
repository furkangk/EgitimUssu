# P02 — E-posta Altyapısı ve Hesap Kurtarma Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Şifre sıfırlama ve e-posta doğrulamayı **uçtan uca** çalışır hale getirmek: sağlayıcı-agnostik `IEmailSender` altyapısı, HTML/metin şablonlar, Identity entegrasyonu, `GET /me` ucu ve mobilde "şifremi unuttum" + "e-posta doğrulama" akışları.

**Architecture:** `Shared/Infrastructure/Email` altında `IEmailSender` soyutlaması; iki implementasyon — `LoggingEmailSender` (dev/test, e-postayı log'a basar) ve `SmtpEmailSender` (MailKit, herhangi bir SMTP relay: Gmail/SES/SendGrid/Resend). Seçim `Email:Provider` konfigürasyonuyla yapılır, prod'da eksik konfig `EmailOptionsGuard` ile fail-fast. Identity'deki `NullIdentityNotificationService` yerine şablon derleyip `IEmailSender`'a veren `EmailIdentityNotificationService` gelir. Mobilde token, e-postadaki `egitimussu://` derin bağlantısından **veya** elle yapıştırmayla girilir.

**Tech Stack:** .NET 9, MailKit 4.x, Razor'suz basit string şablonlama (`EmailTemplates`), xUnit, Flutter (go_router deep link, flutter_bloc).

**Spec:** `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md` (karar **K-01**)

## Global Constraints

- **Sağlayıcı bağımsızlığı:** Identity modülü `IEmailSender`'ı bilmez; yalnız `IIdentityNotificationService`'i çağırır. Sağlayıcıya özel tip hiçbir `Application` katmanına sızmaz.
- **Sır repoda yok:** SMTP kullanıcı adı/parolası yalnız env (`Email__Smtp__Username`, `Email__Smtp__Password`).
- **Fail-fast:** `Email:Provider = "Smtp"` iken host/port/kullanıcı eksikse uygulama açılmaz. `Provider = "Logging"` prod'da uyarı log'lar ama açılır (kasıtlı geçiş dönemi).
- **Enumeration engeli korunur:** `POST /password-reset/request` kayıtlı olmayan e-postada da **200** döner; e-posta yalnız kullanıcı varsa gider (mevcut davranış bozulmaz).
- **Zaman:** `IClock.UtcNow`. **Kimlik:** `IIdGenerator.New()`. **Sonuç:** `Result`/`Result<T>`.
- **Test komutları:** `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj` · `cd mobile && flutter test`.
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: `IEmailSender` soyutlaması + `LoggingEmailSender` + konfigürasyon guard'ı

**Files:**
- Create: `src/Shared/Infrastructure/Email/IEmailSender.cs`
- Create: `src/Shared/Infrastructure/Email/EmailMessage.cs`
- Create: `src/Shared/Infrastructure/Email/LoggingEmailSender.cs`
- Create: `src/Shared/Infrastructure/Configuration/EmailOptions.cs`
- Create: `src/Shared/Infrastructure/Configuration/EmailOptionsGuard.cs`
- Modify: `src/Shared/Infrastructure/ServiceCollectionExtensions.cs` (`AddSharedInfrastructure` içine e-posta kaydı)
- Modify: `src/API.Host/appsettings.json`, `src/API.Host/appsettings.Development.json`
- Test: `tests/Unit/EmailOptionsGuardTests.cs`

**Interfaces:**
- Produces:
  - `sealed record EmailMessage(string ToEmail, string ToName, string Subject, string HtmlBody, string TextBody)`
  - `interface IEmailSender { Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default); }`
  - `sealed class EmailOptions { public string Provider {get;set;} = "Logging"; public string FromEmail {get;set;} = ""; public string FromName {get;set;} = "EğitimÜssü"; public SmtpOptions Smtp {get;set;} = new(); public string AppLinkScheme {get;set;} = "egitimussu"; }`
  - `sealed class SmtpOptions { public string Host {get;set;} = ""; public int Port {get;set;} = 587; public bool UseStartTls {get;set;} = true; public string Username {get;set;} = ""; public string Password {get;set;} = ""; }`
  - `static class EmailOptionsGuard { public static void Validate(EmailOptions options, bool isDevelopment); }`

- [ ] **Step 1: Başarısız guard testini yaz**

`tests/Unit/EmailOptionsGuardTests.cs`:
```csharp
using EgitimUssu.Shared.Infrastructure.Configuration;
using Xunit;

namespace EgitimUssu.Tests.Unit;

public sealed class EmailOptionsGuardTests
{
    [Fact]
    public void Smtp_Without_Host_Should_Throw()
    {
        var options = new EmailOptions { Provider = "Smtp", FromEmail = "no-reply@egitimussu.com" };
        Assert.Throws<InvalidOperationException>(() => EmailOptionsGuard.Validate(options, isDevelopment: false));
    }

    [Fact]
    public void Smtp_Without_FromEmail_Should_Throw()
    {
        var options = new EmailOptions
        {
            Provider = "Smtp",
            Smtp = new SmtpOptions { Host = "smtp.example.com", Username = "u", Password = "p" }
        };
        Assert.Throws<InvalidOperationException>(() => EmailOptionsGuard.Validate(options, isDevelopment: false));
    }

    [Fact]
    public void Complete_Smtp_Should_Pass()
    {
        var options = new EmailOptions
        {
            Provider = "Smtp",
            FromEmail = "no-reply@egitimussu.com",
            Smtp = new SmtpOptions { Host = "smtp.example.com", Port = 587, Username = "u", Password = "p" }
        };
        EmailOptionsGuard.Validate(options, isDevelopment: false);
    }

    [Fact]
    public void Logging_Provider_Should_Pass_Anywhere()
        => EmailOptionsGuard.Validate(new EmailOptions { Provider = "Logging" }, isDevelopment: false);
}
```

- [ ] **Step 2: Çalıştır, kırmızı gör**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~EmailOptionsGuardTests"`
Expected: FAIL — tipler yok.

- [ ] **Step 3: Sözleşme ve seçenek tiplerini yaz**

`src/Shared/Infrastructure/Email/EmailMessage.cs`:
```csharp
namespace EgitimUssu.Shared.Infrastructure.Email;

/// <summary>Gönderilecek tek bir e-posta. Sağlayıcıdan bağımsızdır.</summary>
public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody);
```

`src/Shared/Infrastructure/Email/IEmailSender.cs`:
```csharp
namespace EgitimUssu.Shared.Infrastructure.Email;

/// <summary>
/// E-posta gönderimi. Uygulama katmanları yalnız bu arayüzü görür; SMTP/API detayı Infrastructure'da kalır.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
```

`src/Shared/Infrastructure/Configuration/EmailOptions.cs`:
```csharp
namespace EgitimUssu.Shared.Infrastructure.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"Logging" (dev/test) veya "Smtp".</summary>
    public string Provider { get; set; } = "Logging";

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "EğitimÜssü";

    /// <summary>Mobil derin bağlantı şeması: e-postadaki bağlantı "egitimussu://..." ile başlar.</summary>
    public string AppLinkScheme { get; set; } = "egitimussu";

    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
```

`src/Shared/Infrastructure/Configuration/EmailOptionsGuard.cs`:
```csharp
namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// E-posta konfigürasyonunu startup'ta doğrular (JwtSigningKeyGuard deseni).
/// SMTP seçiliyse eksik ayarla uygulama açılmaz — "sessizce e-posta göndermeme" durumunu engeller.
/// </summary>
public static class EmailOptionsGuard
{
    public static void Validate(EmailOptions options, bool isDevelopment)
    {
        if (!string.Equals(options.Provider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.FromEmail))
        {
            throw new InvalidOperationException("Email:FromEmail zorunludur (Provider=Smtp).");
        }

        if (string.IsNullOrWhiteSpace(options.Smtp.Host))
        {
            throw new InvalidOperationException("Email:Smtp:Host zorunludur (Provider=Smtp).");
        }

        if (options.Smtp.Port <= 0)
        {
            throw new InvalidOperationException("Email:Smtp:Port geçersiz.");
        }

        if (!isDevelopment && string.IsNullOrWhiteSpace(options.Smtp.Username))
        {
            throw new InvalidOperationException(
                "Email:Smtp:Username zorunludur. Değeri Email__Smtp__Username ortam değişkeniyle verin.");
        }
    }
}
```

`src/Shared/Infrastructure/Email/LoggingEmailSender.cs`:
```csharp
using EgitimUssu.Shared.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Email;

/// <summary>
/// E-postayı göndermez, log'a basar. Geliştirme ve testte varsayılan.
/// Üretimde seçilirse startup'ta uyarı basılır (sessiz kayıp olmasın).
/// </summary>
internal sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "E-POSTA (gönderilmedi, Provider=Logging) → {To} | Konu: {Subject}\n{Body}",
            message.ToEmail, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Testi çalıştır**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~EmailOptionsGuardTests"`
Expected: PASS (4 test).

- [ ] **Step 5: DI kaydı + konfigürasyon**

`src/Shared/Infrastructure/ServiceCollectionExtensions.cs` → `AddSharedInfrastructure` içine (mevcut `Configure<...>` kayıtlarının yanına):
```csharp
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddScoped<IEmailSender>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EmailOptions>>().Value;
            return string.Equals(options.Provider, "Smtp", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider)
                : ActivatorUtilities.CreateInstance<LoggingEmailSender>(provider);
        });
```
> `SmtpEmailSender` Task 2'de eklenecek; bu adımda derleme hatası almamak için Task 2'yi aynı görev bloğunda tamamla ya da geçici olarak yalnız `LoggingEmailSender` kaydet ve Task 2'de bu satırı tamamla.

`src/API.Host/appsettings.json` → kök seviyeye:
```json
  "Email": {
    "Provider": "Logging",
    "FromEmail": "",
    "FromName": "EğitimÜssü",
    "AppLinkScheme": "egitimussu",
    "Smtp": { "Host": "", "Port": 587, "UseStartTls": true, "Username": "", "Password": "" }
  },
```
`src/API.Host/Program.cs` → `ConnectionStringGuard` çağrısının ardına:
```csharp
EmailOptionsGuard.Validate(
    builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions(),
    builder.Environment.IsDevelopment());
```

- [ ] **Step 6: Commit**

```bash
git add src/Shared/Infrastructure/Email src/Shared/Infrastructure/Configuration src/API.Host tests/Unit/EmailOptionsGuardTests.cs
git commit -m "feat(email): saglayici-agnostik IEmailSender + Logging implementasyonu + config guard"
```

---

### Task 2: `SmtpEmailSender` (MailKit)

**Files:**
- Create: `src/Shared/Infrastructure/Email/SmtpEmailSender.cs`
- Modify: `src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj` (MailKit paketi)
- Modify: `src/Shared/Infrastructure/ServiceCollectionExtensions.cs` (Task 1 Step 5'teki kaydı tamamla)
- Test: `tests/Unit/SmtpEmailSenderTests.cs`

**Interfaces:**
- Consumes: `EmailOptions`, `EmailMessage`, `IEmailSender`.
- Produces: `internal sealed class SmtpEmailSender : IEmailSender` — ctor `(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger, ISmtpClientFactory clientFactory)`.
- Produces: `interface ISmtpClientFactory { IMailTransport Create(); }` + `MailKitSmtpClientFactory` — testte sahte transport enjekte edilebilsin diye.

- [ ] **Step 1: MailKit paketini ekle**

Run: `dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package MailKit`
Expected: `MailKit` `PackageReference` olarak eklenir.

- [ ] **Step 2: Başarısız testi yaz**

`tests/Unit/SmtpEmailSenderTests.cs`:
```csharp
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Email;
using MailKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Xunit;

namespace EgitimUssu.Tests.Unit;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_Should_Build_Message_With_From_To_Subject_And_Both_Bodies()
    {
        var transport = new RecordingMailTransport();
        var options = Options.Create(new EmailOptions
        {
            Provider = "Smtp",
            FromEmail = "no-reply@egitimussu.com",
            FromName = "EğitimÜssü",
            Smtp = new SmtpOptions { Host = "smtp.example.com", Port = 587, Username = "u", Password = "p" }
        });

        var sender = new SmtpEmailSender(options, NullLogger<SmtpEmailSender>.Instance,
            new StubSmtpClientFactory(transport));

        await sender.SendAsync(new EmailMessage(
            "ogretmen@example.com", "Ayse Yilmaz", "Sifre sifirlama", "<p>merhaba</p>", "merhaba"));

        var sent = Assert.Single(transport.SentMessages);
        Assert.Equal("Sifre sifirlama", sent.Subject);
        Assert.Contains(sent.To.Mailboxes, m => m.Address == "ogretmen@example.com");
        Assert.Contains(sent.From.Mailboxes, m => m.Address == "no-reply@egitimussu.com");
        Assert.Equal("<p>merhaba</p>", sent.HtmlBody);
        Assert.Equal("merhaba", sent.TextBody);
        Assert.True(transport.Connected);
        Assert.True(transport.Authenticated);
    }
}
```
`RecordingMailTransport` ve `StubSmtpClientFactory`'yi aynı dosyanın altına yaz: `RecordingMailTransport`, `MailKit.IMailTransport`'un `SendAsync`, `ConnectAsync`, `AuthenticateAsync`, `DisconnectAsync` üyelerini kaydeden minimal bir sahte olsun (kullanılmayan üyeler `NotSupportedException` fırlatabilir).

- [ ] **Step 3: Çalıştır, kırmızı gör**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~SmtpEmailSenderTests"`
Expected: FAIL — `SmtpEmailSender` yok.

- [ ] **Step 4: `SmtpEmailSender` ve fabrikayı yaz**

`src/Shared/Infrastructure/Email/SmtpEmailSender.cs`:
```csharp
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

internal sealed class MailKitSmtpClientFactory : ISmtpClientFactory
{
    public IMailTransport Create() => new SmtpClient();
}

/// <summary>
/// Herhangi bir SMTP relay'i (Gmail, Amazon SES, SendGrid, Resend …) üzerinden e-posta gönderir.
/// Sağlayıcı değişimi yalnız konfigürasyon değişikliğidir (karar K-01).
/// </summary>
internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly ISmtpClientFactory _clientFactory;

    public SmtpEmailSender(
        IOptions<EmailOptions> options,
        ILogger<SmtpEmailSender> logger,
        ISmtpClientFactory clientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _clientFactory = clientFactory;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = _clientFactory.Create();
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
            // Yutma yok: çağıran akış (ör. kayıt) bozulmasın diye yükseltmiyoruz ama hata görünür kalıyor.
            _logger.LogError(exception, "E-posta gönderilemedi: {To} / {Subject}", message.ToEmail, message.Subject);
            throw;
        }
    }
}
```

- [ ] **Step 5: DI kaydını tamamla**

`ServiceCollectionExtensions.cs`:
```csharp
        services.AddSingleton<ISmtpClientFactory, MailKitSmtpClientFactory>();
```
ve Task 1 Step 5'teki `IEmailSender` fabrika kaydını (Smtp dalı dahil) etkinleştir.

- [ ] **Step 6: Testi çalıştır**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~SmtpEmailSenderTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Shared/Infrastructure tests/Unit/SmtpEmailSenderTests.cs
git commit -m "feat(email): MailKit tabanli SmtpEmailSender"
```

---

### Task 3: Identity entegrasyonu — şablonlar + `EmailIdentityNotificationService`

**Files:**
- Create: `src/Modules/Identity/Infrastructure/EmailTemplates.cs`
- Create: `src/Modules/Identity/Infrastructure/EmailIdentityNotificationService.cs`
- Modify: `src/Modules/Identity/Infrastructure/IdentityRepositoryAndSecurity.cs:132-136` (`NullIdentityNotificationService`'i sil)
- Modify: `src/Modules/Identity/Infrastructure/DependencyInjection.cs` (kaydı değiştir)
- Modify: `src/Modules/Identity/Infrastructure/EgitimUssu.Modules.Identity.Infrastructure.csproj` (Shared.Infrastructure referansı yoksa ekle)
- Test: `tests/Unit/EmailIdentityNotificationServiceTests.cs`

**Interfaces:**
- Consumes: `IIdentityNotificationService` (`IdentityFeatures.cs:41-44`), `IEmailSender`, `EmailOptions`.
- Produces:
  - `static class EmailTemplates` — `static EmailMessage PasswordReset(string toEmail, string token, string appLinkScheme)`, `static EmailMessage EmailVerification(string toEmail, string token, string appLinkScheme)`.
  - `internal sealed class EmailIdentityNotificationService : IIdentityNotificationService`.

- [ ] **Step 1: Başarısız testi yaz**

`tests/Unit/EmailIdentityNotificationServiceTests.cs`:
```csharp
using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Email;
using Microsoft.Extensions.Options;
using Xunit;

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

    [Fact]
    public async Task PasswordReset_Should_Contain_Token_And_Deep_Link()
    {
        var sender = new RecordingEmailSender();
        var service = new EmailIdentityNotificationService(
            sender, Options.Create(new EmailOptions { AppLinkScheme = "egitimussu" }));

        await service.SendPasswordResetAsync("ogrenci@example.com", "TOKEN123", CancellationToken.None);

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
        var service = new EmailIdentityNotificationService(
            sender, Options.Create(new EmailOptions { AppLinkScheme = "egitimussu" }));

        await service.SendEmailVerificationAsync("veli@example.com", "ABC999", CancellationToken.None);

        var message = Assert.Single(sender.Sent);
        Assert.Contains("egitimussu://verify-email?token=ABC999", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("ABC999", message.TextBody, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Çalıştır, kırmızı gör**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~EmailIdentityNotificationServiceTests"`
Expected: FAIL.

- [ ] **Step 3: Şablonları yaz**

`src/Modules/Identity/Infrastructure/EmailTemplates.cs`:
```csharp
using EgitimUssu.Shared.Infrastructure.Email;

namespace EgitimUssu.Modules.Identity.Infrastructure;

/// <summary>
/// Kimlik akışlarının e-posta şablonları. Basit string şablon: harici motor bağımlılığı yok,
/// içerik testlerle sabitlenir.
/// </summary>
internal static class EmailTemplates
{
    private const string BrandName = "EğitimÜssü";

    public static EmailMessage PasswordReset(string toEmail, string token, string appLinkScheme)
    {
        var link = $"{appLinkScheme}://password-reset?token={token}";
        var html = $"""
            <div style="font-family:system-ui,Segoe UI,Roboto,sans-serif;color:#0f172a">
              <h2 style="color:#082B4F">{BrandName} — Şifre sıfırlama</h2>
              <p>Şifreni sıfırlamak için aşağıdaki bağlantıya dokun:</p>
              <p><a href="{link}" style="background:#082B4F;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none">Şifremi sıfırla</a></p>
              <p>Bağlantı açılmazsa uygulamadaki "Şifremi unuttum" ekranına şu kodu yapıştır:</p>
              <p style="font-size:18px;font-weight:700;letter-spacing:1px">{token}</p>
              <p style="color:#64748b;font-size:13px">Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin. Kod 1 saat geçerlidir.</p>
            </div>
            """;
        var text = $"""
            {BrandName} — Şifre sıfırlama

            Kodun: {token}
            Bağlantı: {link}

            Bu isteği sen yapmadıysan yok sayabilirsin. Kod 1 saat geçerlidir.
            """;
        return new EmailMessage(toEmail, toEmail, $"{BrandName} — Şifre sıfırlama", html, text);
    }

    public static EmailMessage EmailVerification(string toEmail, string token, string appLinkScheme)
    {
        var link = $"{appLinkScheme}://verify-email?token={token}";
        var html = $"""
            <div style="font-family:system-ui,Segoe UI,Roboto,sans-serif;color:#0f172a">
              <h2 style="color:#082B4F">{BrandName} — E-posta doğrulama</h2>
              <p>Hesabını etkinleştirmek için aşağıdaki bağlantıya dokun:</p>
              <p><a href="{link}" style="background:#082B4F;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none">E-postamı doğrula</a></p>
              <p>Bağlantı açılmazsa uygulamadaki doğrulama ekranına şu kodu yapıştır:</p>
              <p style="font-size:18px;font-weight:700;letter-spacing:1px">{token}</p>
              <p style="color:#64748b;font-size:13px">Kod 24 saat geçerlidir.</p>
            </div>
            """;
        var text = $"""
            {BrandName} — E-posta doğrulama

            Kodun: {token}
            Bağlantı: {link}

            Kod 24 saat geçerlidir.
            """;
        return new EmailMessage(toEmail, toEmail, $"{BrandName} — E-posta doğrulama", html, text);
    }
}
```

- [ ] **Step 4: Servisi yaz ve `Null...`'ı sil**

`src/Modules/Identity/Infrastructure/EmailIdentityNotificationService.cs`:
```csharp
using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Modules.Identity.Infrastructure;

/// <summary>
/// Kimlik akışı e-postalarını gerçekten gönderir (A-03). Önceki <c>NullIdentityNotificationService</c>
/// hiçbir şey göndermiyordu; şifre sıfırlama ve doğrulama akışları fiilen ölüydü.
/// </summary>
internal sealed class EmailIdentityNotificationService : IIdentityNotificationService
{
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _options;

    public EmailIdentityNotificationService(IEmailSender emailSender, IOptions<EmailOptions> options)
    {
        _emailSender = emailSender;
        _options = options.Value;
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken)
        => _emailSender.SendAsync(EmailTemplates.PasswordReset(email, token, _options.AppLinkScheme), cancellationToken);

    public Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken)
        => _emailSender.SendAsync(EmailTemplates.EmailVerification(email, token, _options.AppLinkScheme), cancellationToken);
}
```
`IdentityRepositoryAndSecurity.cs:132-136` içindeki `NullIdentityNotificationService` sınıfını **sil**.
`DependencyInjection.cs` içindeki kaydı değiştir:
```csharp
        services.AddScoped<IIdentityNotificationService, EmailIdentityNotificationService>();
```

- [ ] **Step 5: Testi çalıştır**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~EmailIdentityNotificationServiceTests"`
Expected: PASS (2 test).

- [ ] **Step 6: Uçtan uca elle doğrula (Provider=Logging)**

Run: `dotnet run --project src/API.Host` (ayrı terminalde)
Run:
```bash
curl -s -X POST http://localhost:5296/api/identity/password-reset/request \
  -H 'Content-Type: application/json' -d '{"email":"diag1@example.com"}' -o /dev/null -w "%{http_code}\n"
```
Expected: `200`; API log'unda `E-POSTA (gönderilmedi, Provider=Logging) → diag1@example.com | Konu: EğitimÜssü — Şifre sıfırlama` satırı ve içinde token görünür.

- [ ] **Step 7: Commit**

```bash
git add src/Modules/Identity tests/Unit/EmailIdentityNotificationServiceTests.cs
git commit -m "feat(identity): gercek e-posta gonderimi (sifre sifirlama + dogrulama) (A-03)"
```

---

### Task 4: `GET /api/identity/me` (M01-1)

**Files:**
- Modify: `src/Modules/Identity/API/IdentityModule.cs`
- Modify: `src/Modules/Identity/Application/IdentityFeatures.cs` (query + handler)
- Modify: `src/Modules/Identity/Application/IdentityPolicies.cs` (authorizer)
- Modify: `src/Modules/Identity/Infrastructure/DependencyInjection.cs`
- Test: `tests/Integration/TeacherWorkflowIntegrationTests.cs` (yeni test metodu)

**Interfaces:**
- Produces:
  - `sealed record GetCurrentUserQuery() : IQuery<Result<CurrentUserResponse>>`
  - `sealed record CurrentUserResponse(Guid UserId, string Email, string FullName, IReadOnlyCollection<string> Roles, bool IsEmailConfirmed, string Status)`
  - `GET /api/identity/me` (auth `AuthenticatedUser`)

- [ ] **Step 1: Başarısız integration testini yaz**

`tests/Integration/TeacherWorkflowIntegrationTests.cs` içine, mevcut testlerin env kurulum desenini birebir izleyen yeni bir `[Fact]` ekle:
```csharp
    [Fact]
    public async Task Me_Should_Return_Authenticated_User()
    {
        // ... mevcut testlerdeki gibi env ayarla + WebApplicationFactory + register ...
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var meResponse = await client.GetAsync("/api/identity/me");
        meResponse.EnsureSuccessStatusCode();
        var me = await ReadJsonAsync(meResponse);

        Assert.Equal(userId, me.GetProperty("userId").GetGuid());
        Assert.Equal("me1@example.com", me.GetProperty("email").GetString());
        Assert.Contains("Teacher", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }
```
Ayrıca kimliksiz istekte 401 dönmesini doğrulayan ikinci bir assert ekle (`client` üzerinden Authorization header'ı temizleyip `Assert.Equal(HttpStatusCode.Unauthorized, ...)`).

- [ ] **Step 2: Çalıştır, kırmızı gör**

Run: `dotnet test tests/Integration/EgitimUssu.Tests.Integration.csproj --filter "FullyQualifiedName~Me_Should_Return_Authenticated_User"`
Expected: FAIL — 404.

- [ ] **Step 3: Query + handler + authorizer yaz**

`IdentityFeatures.cs` (dosya sonuna):
```csharp
public sealed record GetCurrentUserQuery : IQuery<Result<CurrentUserResponse>>;

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyCollection<string> Roles,
    bool IsEmailConfirmed,
    string Status);

public sealed class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>
{
    private static readonly Error NotFound = new("identity.user_not_found", "Kullanıcı bulunamadı.");
    private readonly IUserAccountRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetCurrentUserQueryHandler(IUserAccountRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<CurrentUserResponse>.Failure(NotFound);
        }

        var user = await _repository.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? Result<CurrentUserResponse>.Failure(NotFound)
            : Result<CurrentUserResponse>.Success(new CurrentUserResponse(
                user.Id, user.Email, user.FullName,
                user.Roles.Select(role => role.ToString()).ToArray(),
                user.IsEmailConfirmed, user.Status.ToString()));
    }
}
```
> `IUserAccountRepository.GetByIdAsync` yoksa arayüze ekle ve `UserAccountRepository`'de uygula. `user.FullName`/`user.Roles` gerçek alan adları için `IdentityDomainModel.cs`'e bak, birebir uyarla.

`IdentityPolicies.cs`:
```csharp
public sealed class GetCurrentUserQueryAuthorizer : IQueryAuthorizer<GetCurrentUserQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlem için giriş yapmalısınız.");
    private readonly ICurrentUser _currentUser;

    public GetCurrentUserQueryAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<Result> Authorize(GetCurrentUserQuery query, CancellationToken cancellationToken)
        => Task.FromResult(_currentUser.IsAuthenticated ? Result.Success() : Result.Failure(Forbidden));
}
```

- [ ] **Step 4: Endpoint'i ve DI'yı bağla**

`IdentityModule.cs` → `MapEndpoints` içine:
```csharp
        group.MapGet("/me", GetCurrentUserAsync)
            .WithSummary("Oturum açmış kullanıcının bilgilerini döndürür")
            .RequireAuthorization("AuthenticatedUser");
```
ve metot:
```csharp
    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context, IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetCurrentUserQuery(), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error);
    }
```
`DependencyInjection.cs`:
```csharp
        services.AddScoped<IQueryHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>, GetCurrentUserQueryHandler>();
        services.AddScoped<IQueryAuthorizer<GetCurrentUserQuery>, GetCurrentUserQueryAuthorizer>();
```

- [ ] **Step 5: Testi çalıştır**

Run: `dotnet test tests/Integration/EgitimUssu.Tests.Integration.csproj --filter "FullyQualifiedName~Me_Should_Return"`
Expected: PASS.

- [ ] **Step 6: Doküman + commit**

`doc/modules/m01_identity.md` §3.3 → `GET /me` maddesini `- [x]` yap ve endpoint envanterine ekle; `doc/modules/00_genel_bakis.md` Identity bloğuna `GET /me (auth)` satırını ekle.
```bash
git add src/Modules/Identity tests/Integration doc/modules/m01_identity.md doc/modules/00_genel_bakis.md
git commit -m "feat(identity): GET /me ucu (M01-1)"
```

---

### Task 5: Mobil — "Şifremi unuttum" akışı

**Files:**
- Modify: `mobile/lib/features/auth/domain/repositories/auth_repository.dart`
- Modify: `mobile/lib/features/auth/data/repositories/auth_repository_impl.dart`
- Create: `mobile/lib/features/auth/presentation/cubit/password_reset_cubit.dart`
- Create: `mobile/lib/features/auth/presentation/pages/password_reset_request_page.dart`
- Create: `mobile/lib/features/auth/presentation/pages/password_reset_confirm_page.dart`
- Modify: `mobile/lib/core/routing/app_router.dart` (2 rota)
- Modify: `mobile/lib/features/auth/presentation/pages/login_page.dart` ("Şifremi unuttum" bağlantısı)
- Test: `mobile/test/features/auth/presentation/password_reset_cubit_test.dart`
- Create: `doc/pages/auth_password_reset.md`

**Interfaces:**
- Produces (repository):
  - `Future<void> requestPasswordReset({required String email});` → `POST /api/identity/password-reset/request`
  - `Future<void> confirmPasswordReset({required String email, required String token, required String newPassword});` → `POST /api/identity/password-reset/confirm`
- Produces (cubit): `PasswordResetCubit` — durumlar `PasswordResetState(status: idle|sending|sent|confirming|done|failure, message: String?)`; metotlar `requestReset(String email)`, `confirmReset({required String email, required String token, required String newPassword})`.
- Rotalar: `/password-reset` (istek), `/password-reset/confirm` (kod + yeni şifre; `state.extra` ile e-posta ve varsa token taşınır).

- [ ] **Step 1: Cubit testini yaz (kırmızı)**

`mobile/test/features/auth/presentation/password_reset_cubit_test.dart`:
```dart
import 'package:bloc_test/bloc_test.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/password_reset_cubit.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../helpers/fake_auth_repository.dart';

void main() {
  group('PasswordResetCubit', () {
    blocTest<PasswordResetCubit, PasswordResetState>(
      'istek basarili olunca sent durumuna gecer',
      build: () => PasswordResetCubit(FakeAuthRepository()),
      act: (cubit) => cubit.requestReset('ogretmen@example.com'),
      expect: () => <Matcher>[
        isA<PasswordResetState>().having((s) => s.status, 'status', PasswordResetStatus.sending),
        isA<PasswordResetState>().having((s) => s.status, 'status', PasswordResetStatus.sent),
      ],
    );

    blocTest<PasswordResetCubit, PasswordResetState>(
      'onay basarili olunca done durumuna gecer',
      build: () => PasswordResetCubit(FakeAuthRepository()),
      act: (cubit) => cubit.confirmReset(
        email: 'ogretmen@example.com',
        token: 'TOKEN123',
        newPassword: 'YeniSifre123!',
      ),
      expect: () => <Matcher>[
        isA<PasswordResetState>().having((s) => s.status, 'status', PasswordResetStatus.confirming),
        isA<PasswordResetState>().having((s) => s.status, 'status', PasswordResetStatus.done),
      ],
    );
  });
}
```
> `bloc_test` `dev_dependencies`'te yoksa: `cd mobile && flutter pub add --dev bloc_test`.
> `FakeAuthRepository`'ye (P01 Task 2) `requestPasswordReset` / `confirmPasswordReset` gövdeleri eklenir.

- [ ] **Step 2: Çalıştır, kırmızı gör**

Run: `cd mobile && flutter test test/features/auth/presentation/password_reset_cubit_test.dart`
Expected: FAIL — `PasswordResetCubit` yok.

- [ ] **Step 3: Repository metotlarını ekle**

`auth_repository.dart` (arayüz):
```dart
  Future<void> requestPasswordReset({required String email});

  Future<void> confirmPasswordReset({
    required String email,
    required String token,
    required String newPassword,
  });
```
`auth_repository_impl.dart`:
```dart
  @override
  Future<void> requestPasswordReset({required String email}) async {
    await _apiClient.post(
      '/api/identity/password-reset/request',
      data: <String, dynamic>{'email': email},
    );
  }

  @override
  Future<void> confirmPasswordReset({
    required String email,
    required String token,
    required String newPassword,
  }) async {
    await _apiClient.post(
      '/api/identity/password-reset/confirm',
      data: <String, dynamic>{'email': email, 'token': token, 'newPassword': newPassword},
    );
  }
```
> `_apiClient.post` imzası için `mobile/lib/core/network/api_client.dart`'a bak; gövde alan adları backend `ResetPasswordCommand(string Email, string Token, string NewPassword)` ile birebir olmalı.

- [ ] **Step 4: Cubit'i yaz**

`mobile/lib/features/auth/presentation/cubit/password_reset_cubit.dart`:
```dart
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

enum PasswordResetStatus { idle, sending, sent, confirming, done, failure }

class PasswordResetState extends Equatable {
  const PasswordResetState({this.status = PasswordResetStatus.idle, this.message});

  final PasswordResetStatus status;
  final String? message;

  PasswordResetState copyWith({PasswordResetStatus? status, String? message}) =>
      PasswordResetState(status: status ?? this.status, message: message);

  @override
  List<Object?> get props => <Object?>[status, message];
}

class PasswordResetCubit extends Cubit<PasswordResetState> {
  PasswordResetCubit(this._repository) : super(const PasswordResetState());

  final AuthRepository _repository;

  Future<void> requestReset(String email) async {
    emit(state.copyWith(status: PasswordResetStatus.sending));
    try {
      await _repository.requestPasswordReset(email: email);
      emit(state.copyWith(status: PasswordResetStatus.sent));
    } on ApiException catch (error) {
      emit(state.copyWith(status: PasswordResetStatus.failure, message: error.message));
    }
  }

  Future<void> confirmReset({
    required String email,
    required String token,
    required String newPassword,
  }) async {
    emit(state.copyWith(status: PasswordResetStatus.confirming));
    try {
      await _repository.confirmPasswordReset(email: email, token: token, newPassword: newPassword);
      emit(state.copyWith(status: PasswordResetStatus.done));
    } on ApiException catch (error) {
      emit(state.copyWith(status: PasswordResetStatus.failure, message: error.message));
    }
  }
}
```

- [ ] **Step 5: Testi çalıştır**

Run: `cd mobile && flutter test test/features/auth/presentation/password_reset_cubit_test.dart`
Expected: PASS.

- [ ] **Step 6: İki ekranı yaz**

`password_reset_request_page.dart`: e-posta alanı + "Sıfırlama kodu gönder" butonu; `sent` durumunda bilgilendirme + `context.push('/password-reset/confirm', extra: email)`.
`password_reset_confirm_page.dart`: kod alanı + yeni şifre + tekrar alanı; `done` durumunda `context.go('/login')` + `SnackBar('Şifren güncellendi, giriş yapabilirsin.')`.

Her iki ekran da `doc/architecture/design_system.md` token'larını ve `mobile/lib/shared/widgets/` içindeki mevcut ortak alan/buton widget'larını kullanır — yeni stil icat etme; en yakın örnek `login_page.dart`.

Metinler Türkçe ve **tam Türkçe karakterli**: "Şifremi unuttum", "E-posta adresin", "Sıfırlama kodu", "Yeni şifre".

- [ ] **Step 7: Rotaları ve giriş bağlantısını bağla**

`app_router.dart` → `/login` rotasının hemen ardına:
```dart
        GoRoute(
          path: '/password-reset',
          builder: (context, state) => const PasswordResetRequestPage(),
        ),
        GoRoute(
          path: '/password-reset/confirm',
          builder: (context, state) => PasswordResetConfirmPage(
            email: state.extra is String ? state.extra as String : '',
          ),
        ),
```
`login_page.dart` → şifre alanının altına:
```dart
            TextButton(
              onPressed: () => context.push('/password-reset'),
              child: const Text('Şifremi unuttum'),
            ),
```

- [ ] **Step 8: Tüm mobil testleri koştur**

Run: `cd mobile && flutter test && flutter analyze`
Expected: PASS + sorun yok.

- [ ] **Step 9: Sayfa dokümanı + commit**

`doc/pages/auth_password_reset.md` oluştur (mevcut `doc/pages/auth_login.md` şablonunu izle: amaç, rota, state, API, boş/hata durumları) ve `doc/pages/00_pages_index.md`'ye iki satır ekle.
```bash
git add mobile/lib mobile/test doc/pages
git commit -m "feat(mobile): sifremi unuttum akisi (D-01)"
```

---

### Task 6: Mobil — E-posta doğrulama ekranı + derin bağlantı

**Files:**
- Create: `mobile/lib/features/auth/presentation/pages/email_verification_page.dart`
- Modify: `mobile/lib/features/auth/domain/repositories/auth_repository.dart` + `..._impl.dart`
- Modify: `mobile/lib/core/routing/app_router.dart` (`/verify-email` rotası)
- Modify: `mobile/android/app/src/main/AndroidManifest.xml` (intent-filter)
- Modify: `mobile/ios/Runner/Info.plist` (`CFBundleURLTypes`)
- Test: `mobile/test/core/routing/deep_link_test.dart`
- Create: `doc/pages/auth_email_verification.md`

**Interfaces:**
- Produces (repository):
  - `Future<void> requestEmailVerification({required String email});` → `POST /api/identity/email-verification/request`
  - `Future<void> confirmEmailVerification({required String email, required String token});` → `POST /api/identity/email-verification/confirm`
- Rota: `/verify-email?token=...` — `state.uri.queryParameters['token']` ile token okunur; boşsa kullanıcı elle girer.

- [ ] **Step 1: Derin bağlantı testini yaz (kırmızı)**

`mobile/test/core/routing/deep_link_test.dart`:
```dart
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/email_verification_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

void main() {
  testWidgets('egitimussu://verify-email?token=ABC dogrulama ekranini token ile acar', (tester) async {
    final router = GoRouter(
      initialLocation: '/verify-email?token=ABC123',
      routes: <RouteBase>[
        GoRoute(
          path: '/verify-email',
          builder: (context, state) => EmailVerificationPage(
            initialToken: state.uri.queryParameters['token'] ?? '',
          ),
        ),
      ],
    );

    await tester.pumpWidget(MaterialApp.router(routerConfig: router));
    await tester.pumpAndSettle();

    expect(find.text('ABC123'), findsOneWidget);
  });
}
```

- [ ] **Step 2: Çalıştır, kırmızı gör**

Run: `cd mobile && flutter test test/core/routing/deep_link_test.dart`
Expected: FAIL — `EmailVerificationPage` yok.

- [ ] **Step 3: Repository metotlarını ekle**

`auth_repository.dart` + `_impl.dart`:
```dart
  @override
  Future<void> requestEmailVerification({required String email}) async {
    await _apiClient.post(
      '/api/identity/email-verification/request',
      data: <String, dynamic>{'email': email},
    );
  }

  @override
  Future<void> confirmEmailVerification({required String email, required String token}) async {
    await _apiClient.post(
      '/api/identity/email-verification/confirm',
      data: <String, dynamic>{'email': email, 'token': token},
    );
  }
```

- [ ] **Step 4: Ekranı yaz**

`email_verification_page.dart` — `initialToken` ile açılır; token alanı `TextEditingController(text: initialToken)` ile dolar (test bu metni arar), "Doğrula" butonu `confirmEmailVerification` çağırır, başarıda `context.go('/login')` + başarı SnackBar'ı, "Kodu tekrar gönder" butonu `requestEmailVerification` çağırır.

- [ ] **Step 5: Rotayı ekle**

`app_router.dart`:
```dart
        GoRoute(
          path: '/verify-email',
          builder: (context, state) => EmailVerificationPage(
            initialToken: state.uri.queryParameters['token'] ?? '',
          ),
        ),
```

- [ ] **Step 6: Platform derin bağlantı tanımları**

`mobile/android/app/src/main/AndroidManifest.xml` → ana `<activity>` içine:
```xml
            <intent-filter>
                <action android:name="android.intent.action.VIEW" />
                <category android:name="android.intent.category.DEFAULT" />
                <category android:name="android.intent.category.BROWSABLE" />
                <data android:scheme="egitimussu" />
            </intent-filter>
```
`mobile/ios/Runner/Info.plist` → `<dict>` içine:
```xml
	<key>CFBundleURLTypes</key>
	<array>
		<dict>
			<key>CFBundleURLName</key>
			<string>com.egitimussu.app</string>
			<key>CFBundleURLSchemes</key>
			<array>
				<string>egitimussu</string>
			</array>
		</dict>
	</array>
```

- [ ] **Step 7: Testleri koştur**

Run: `cd mobile && flutter test && flutter analyze`
Expected: PASS + sorun yok.

- [ ] **Step 8: Cihazda derin bağlantıyı doğrula**

Run (Android emülatör açıkken):
```bash
adb shell am start -W -a android.intent.action.VIEW -d "egitimussu://verify-email?token=ABC123" com.egitimussu.app
```
Expected: Uygulama açılır, doğrulama ekranında token alanı `ABC123` doludur.
(Paket adı farklıysa `mobile/android/app/build.gradle.kts` içindeki `applicationId`'yi kullan.)

- [ ] **Step 9: Doküman + commit**

`doc/pages/auth_email_verification.md` oluştur, `doc/pages/00_pages_index.md`'ye satır ekle, `doc/modules/m01_identity.md` mobil kontrol listesindeki ilgili maddeleri `- [x]` yap.
```bash
git add mobile doc/pages doc/modules/m01_identity.md
git commit -m "feat(mobile): e-posta dogrulama ekrani + derin baglanti (D-02)"
```

---

### Task 7: Kapanış — uçtan uca doğrulama ve doküman

- [ ] **Step 1: Gerçek SMTP ile bir kez dene (staging/geliştirici hesabı)**

```bash
Email__Provider=Smtp \
Email__FromEmail="no-reply@<alanadin>" \
Email__Smtp__Host=<smtp-host> \
Email__Smtp__Port=587 \
Email__Smtp__Username=<kullanici> \
Email__Smtp__Password=<parola> \
dotnet run --project src/API.Host
```
Sonra `password-reset/request` çağır; gerçek posta kutusuna e-posta düşmeli, bağlantı `egitimussu://password-reset?token=...` olmalı.
Expected: e-posta ulaştı, mobil uygulamada bağlantı doğrulama ekranını açıyor.

- [ ] **Step 2: Tam test paketleri**

Run: `dotnet test EgitimUssu.slnx && cd mobile && flutter test`
Expected: başarısız 0.

- [ ] **Step 3: Dokümanları güncelle**

- `doc/modules/m01_identity.md`: e-posta gönderimi artık gerçek; `GET /me` eklendi; mobil kontrol listesi güncel.
- `doc/modules/00_genel_bakis.md`: Identity endpoint bloğuna `GET /me (auth)`.
- `doc/architecture/backend.md`: "E-posta" başlığı — `IEmailSender`, sağlayıcı seçimi, env değişkenleri.
- `doc/denetim/2026-09-02_eksik_analizi.md`: A-03, M01-1, D-01, D-02 → `✅ (P02)`.
- Her dosyanın altındaki `Güncelleme:` tarihini `2026-09-02` yap.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: P02 e-posta altyapisi kapanisi (A-03/M01-1/D-01/D-02)"
```

---

## Kabul Kriterleri

- [ ] `Email:Provider=Smtp` iken eksik konfigürasyonla uygulama **açılmıyor** (guard testleri yeşil)
- [ ] `Provider=Logging` iken e-posta içeriği log'da tam görünüyor
- [ ] Gerçek SMTP ile şifre sıfırlama e-postası bir kez uçtan uca teslim edildi
- [ ] Mobil: `/password-reset` → kod → yeni şifre → `/login` ile giriş yapılabiliyor
- [ ] Mobil: `egitimussu://verify-email?token=...` bağlantısı uygulamayı doğru ekranda açıyor
- [ ] `GET /api/identity/me` kimlikli 200, kimliksiz 401
- [ ] `dotnet test EgitimUssu.slnx` ve `flutter test` yeşil
