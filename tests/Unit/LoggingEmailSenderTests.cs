using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Email;
using EgitimUssu.Shared.Infrastructure.Observability;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P15 Task 6 / N-24, P-01: <see cref="LoggingEmailSender"/> e-postayı göndermez, log'a yazar — ama
/// gövdeyi <b>hiçbir ortamda</b> yazmaz. Gövde şifre sıfırlama / doğrulama kodunu taşır; log'a düşen
/// tek bir kod, log'a erişen herkese o hesabı teslim eder. Log satırında yalnız maskeli alıcı
/// (<c>a***@g***.com</c>) ve konu bulunur.
/// <para>
/// Tek istisna geliştiricinin kendi makinesidir: <c>Development</c> <b>ve</b>
/// <c>Email:Logging:IncludeBodyInDevelopment=true</c>. Bayrak Staging/Production'da açık olsa bile
/// gövde yazılmaz — staging log'ları ekiple paylaşılır.
/// </para>
/// <para>
/// Host JSON konsol log'u kullanır (<c>AddJsonConsole</c>): yalnız işlenmiş mesaj değil, şablonun
/// <b>yapısal durum</b> değerleri de çıktıya girer. Bu yüzden iki yer birden denetlenir.
/// </para>
/// </summary>
public sealed class LoggingEmailSenderTests
{
    private const string RawRecipient = "ahmet.yilmaz@gmail.com";
    private const string ResetToken = "Zk3+q/9xR2wT8vLm==";
    private const string Subject = "EğitimÜssü — Şifre sıfırlama";

    private static EmailMessage PasswordResetMessage()
    {
        var link = $"egitimussu://password-reset?token={Uri.EscapeDataString(ResetToken)}";
        return new EmailMessage(
            RawRecipient,
            RawRecipient,
            Subject,
            $"<p><a href=\"{link}\">Şifremi sıfırla</a></p><p>{ResetToken}</p>",
            $"Şifreni sıfırlamak için: {link}\nKodun: {ResetToken}");
    }

    [Theory]
    [InlineData("Production", false)]
    [InlineData("Production", true)]
    [InlineData("Staging", false)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public async Task Body_Should_Never_Reach_The_Log_Outside_Opted_In_Development(
        string environmentName, bool includeBodyFlag)
    {
        var logger = new RecordingLogger<LoggingEmailSender>();
        var sender = CreateSender(logger, environmentName, includeBodyFlag);

        await sender.SendAsync(PasswordResetMessage());

        var entry = Assert.Single(logger.Entries);
        foreach (var text in entry.AllText())
        {
            Assert.DoesNotContain(ResetToken, text);
            Assert.DoesNotContain(Uri.EscapeDataString(ResetToken), text);
            Assert.DoesNotContain("password-reset", text);
            Assert.DoesNotContain(RawRecipient, text);
        }
    }

    [Fact]
    public async Task Log_Should_Carry_Masked_Recipient_And_Subject()
    {
        var logger = new RecordingLogger<LoggingEmailSender>();
        var sender = CreateSender(logger, "Production", includeBodyInDevelopment: false);

        await sender.SendAsync(PasswordResetMessage());

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("a***@g***.com", entry.Message);
        Assert.Contains(Subject, entry.Message);
    }

    [Fact]
    public async Task Body_Should_Be_Logged_Only_When_Development_Opts_In()
    {
        var logger = new RecordingLogger<LoggingEmailSender>();
        var sender = CreateSender(logger, "Development", includeBodyInDevelopment: true);

        await sender.SendAsync(PasswordResetMessage());

        var entry = Assert.Single(logger.Entries);
        Assert.Contains(ResetToken, entry.Message);
        // Alıcı geliştirme ortamında da maskelidir; gövde açılsa bile adres tek başına sızmasın.
        Assert.Contains("a***@g***.com", entry.Message);
    }

    [Theory]
    [InlineData("ahmet@domain.com", "a***@d***.com")]
    [InlineData("ahmet.yilmaz@gmail.com", "a***@g***.com")]
    [InlineData("demo.ogrenci1@egitimussu.local", "d***@e***.local")]
    [InlineData("x@okul.k12.tr", "x***@o***.tr")]
    [InlineData("root@localhost", "r***@l***")]
    [InlineData("çağla@örnek.com", "ç***@ö***.com")]
    public void MaskEmail_Should_Keep_Only_First_Letters_And_Top_Level_Domain(string email, string expected)
        => Assert.Equal(expected, SensitiveDataMask.MaskEmail(email));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@domain.com")]
    [InlineData("ahmet@")]
    public void MaskEmail_Should_Fully_Mask_Values_That_Are_Not_An_Address(string? value)
        => Assert.Equal(SensitiveDataMask.Placeholder, SensitiveDataMask.MaskEmail(value));

    private static LoggingEmailSender CreateSender(
        ILogger<LoggingEmailSender> logger, string environmentName, bool includeBodyInDevelopment)
    {
        var options = new EmailOptions
        {
            Logging = new EmailLoggingOptions { IncludeBodyInDevelopment = includeBodyInDevelopment }
        };

        return new LoggingEmailSender(logger, Options.Create(options), new StubHostEnvironment(environmentName));
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "EgitimUssu.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyList<KeyValuePair<string, object?>> State)
    {
        /// <summary>İşlenmiş mesaj + her yapısal durum değeri (JSON konsolun yazdığı her şey).</summary>
        public IEnumerable<string> AllText()
        {
            yield return Message;
            foreach (var (_, value) in State)
            {
                yield return value?.ToString() ?? string.Empty;
            }
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), values));
        }
    }
}
