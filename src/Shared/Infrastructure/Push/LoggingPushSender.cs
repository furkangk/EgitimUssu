using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Push;

/// <summary>
/// Push göndermez, log'a basar. Geliştirme ve testte varsayılan (<c>Push:Provider = "Logging"</c>).
/// Üretimde seçilirse startup'ta uyarı basılır (bkz. <c>Program.cs</c>) — sessiz kayıp olmasın.
/// </summary>
public sealed class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task<PushResult> SendAsync(
        string deviceToken,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        logger.LogInformation(
            "PUSH (gönderilmedi, Provider=Logging) → {Token} | {Title}: {Body} | Veri: {Data}",
            deviceToken,
            message.Title,
            message.Body,
            string.Join(", ", message.Data.Select(pair => $"{pair.Key}={pair.Value}")));

        return Task.FromResult(new PushResult(PushDeliveryStatus.Delivered, null));
    }
}
