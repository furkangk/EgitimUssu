using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Email;
using MailKit;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EgitimUssu.Tests.Unit;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_Should_Build_Message_With_From_To_Subject_And_Both_Bodies()
    {
        var transport = new RecordingMailTransport();
        var sender = CreateSender(transport);

        await sender.SendAsync(new EmailMessage(
            "ogretmen@example.com",
            "Ayse Yilmaz",
            "Sifre sifirlama",
            "<p>merhaba</p>",
            "merhaba"));

        var sent = Assert.Single(transport.SentMessages);
        Assert.Equal("Sifre sifirlama", sent.Subject);
        Assert.Contains(sent.To.Mailboxes, m => m.Address == "ogretmen@example.com");
        Assert.Contains(sent.From.Mailboxes, m => m.Address == "no-reply@egitimussu.com");
        Assert.Equal("<p>merhaba</p>", sent.HtmlBody);
        Assert.Equal("merhaba", sent.TextBody!.Trim());
        Assert.True(transport.ConnectCalled);
        Assert.True(transport.AuthenticateCalled);
        Assert.True(transport.DisconnectCalled);
    }

    [Fact]
    public async Task SendAsync_Should_Connect_With_StartTls_When_Configured()
    {
        var transport = new RecordingMailTransport();
        var sender = CreateSender(transport);

        await sender.SendAsync(new EmailMessage("a@b.com", "A", "k", "<p>x</p>", "x"));

        Assert.Equal("smtp.example.com", transport.Host);
        Assert.Equal(587, transport.Port);
        Assert.Equal(SecureSocketOptions.StartTls, transport.SocketOptions);
    }

    [Fact]
    public async Task SendAsync_Should_Skip_Authentication_When_Username_Is_Empty()
    {
        var transport = new RecordingMailTransport();
        var sender = CreateSender(transport, username: string.Empty);

        await sender.SendAsync(new EmailMessage("a@b.com", "A", "k", "<p>x</p>", "x"));

        Assert.False(transport.AuthenticateCalled);
        Assert.Single(transport.SentMessages);
    }

    [Fact]
    public async Task SendAsync_Should_Rethrow_When_Transport_Fails()
    {
        var transport = new RecordingMailTransport { FailOnSend = true };
        var sender = CreateSender(transport);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(new EmailMessage("a@b.com", "A", "k", "<p>x</p>", "x")));
    }

    private static SmtpEmailSender CreateSender(RecordingMailTransport transport, string username = "u")
    {
        var options = Options.Create(new EmailOptions
        {
            Provider = EmailOptions.SmtpProvider,
            FromEmail = "no-reply@egitimussu.com",
            FromName = "EgitimUssu",
            Smtp = new SmtpOptions
            {
                Host = "smtp.example.com",
                Port = 587,
                UseStartTls = true,
                Username = username,
                Password = "p"
            }
        });

        return new SmtpEmailSender(
            options,
            NullLogger<SmtpEmailSender>.Instance,
            new StubSmtpClientFactory(transport));
    }
}

/// <summary>Testte gercek SMTP baglantisi kurulmasin diye <see cref="RecordingMailTransport"/> dondurur.</summary>
internal sealed class StubSmtpClientFactory(IMailTransport transport) : ISmtpClientFactory
{
    public IMailTransport Create() => transport;
}

/// <summary>
/// MailKit transport'unun minimal sahtesi: yalnizca connect/authenticate/send/disconnect cagrilari kaydedilir.
/// Kullanilmayan uyeler <see cref="NotSupportedException"/> firlatir.
/// </summary>
internal sealed class RecordingMailTransport() : MailTransport(new NullProtocolLogger())
{
    public List<MimeMessage> SentMessages { get; } = [];

    public bool ConnectCalled { get; private set; }

    public bool AuthenticateCalled { get; private set; }

    public bool DisconnectCalled { get; private set; }

    public bool FailOnSend { get; init; }

    public string? Host { get; private set; }

    public int Port { get; private set; }

    public SecureSocketOptions SocketOptions { get; private set; }

    public override object SyncRoot { get; } = new();

    protected override string Protocol => "smtp";

    public override HashSet<string> AuthenticationMechanisms { get; } = ["PLAIN"];

    public override bool IsConnected => ConnectCalled && !DisconnectCalled;

    public override bool IsAuthenticated => AuthenticateCalled;

    public override bool IsSecure => true;

    public override bool IsEncrypted => true;

    public override bool IsSigned => false;

    public override SslProtocols SslProtocol => SslProtocols.Tls12;

    public override TlsCipherSuite? SslCipherSuite => null;

    public override CipherAlgorithmType? SslCipherAlgorithm => null;

    public override int? SslCipherStrength => null;

    public override HashAlgorithmType? SslHashAlgorithm => null;

    public override int? SslHashStrength => null;

    public override ExchangeAlgorithmType? SslKeyExchangeAlgorithm => null;

    public override int? SslKeyExchangeStrength => null;

    public override int Timeout { get; set; } = 30_000;

    public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
    {
        Host = host;
        Port = port;
        SocketOptions = options;
        ConnectCalled = true;
        return Task.CompletedTask;
    }

    public override Task AuthenticateAsync(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
    {
        AuthenticateCalled = true;
        return Task.CompletedTask;
    }

    public override Task<string> SendAsync(
        FormatOptions options,
        MimeMessage message,
        CancellationToken cancellationToken = default,
        ITransferProgress? progress = null)
    {
        if (FailOnSend)
        {
            throw new InvalidOperationException("SMTP relay reddetti.");
        }

        SentMessages.Add(message);
        return Task.FromResult("queued");
    }

    public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
    {
        DisconnectCalled = true;
        return Task.CompletedTask;
    }

    public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override void Connect(Socket socket, string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override Task ConnectAsync(Socket socket, string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override void Connect(Stream stream, string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override Task ConnectAsync(Stream stream, string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override void Authenticate(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override void Authenticate(SaslMechanism mechanism, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override Task AuthenticateAsync(SaslMechanism mechanism, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override void Disconnect(bool quit, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override void NoOp(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override Task NoOpAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override string Send(FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
        => throw new NotSupportedException();

    public override string Send(
        FormatOptions options,
        MimeMessage message,
        MailboxAddress sender,
        IEnumerable<MailboxAddress> recipients,
        CancellationToken cancellationToken = default,
        ITransferProgress? progress = null)
        => throw new NotSupportedException();

    public override Task<string> SendAsync(
        FormatOptions options,
        MimeMessage message,
        MailboxAddress sender,
        IEnumerable<MailboxAddress> recipients,
        CancellationToken cancellationToken = default,
        ITransferProgress? progress = null)
        => throw new NotSupportedException();
}
