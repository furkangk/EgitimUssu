using EgitimUssu.Shared.Infrastructure.Email;
using EgitimUssu.Shared.Infrastructure.Push;

namespace EgitimUssu.Shared.Infrastructure.Observability;

/// <summary>
/// C-04: Push gönderim sonucunu ölçen sarmalayıcı.
/// <para>
/// Ölçüm neden <b>sağlayıcının içine</b> değil de buraya konuldu: her sağlayıcıya
/// (<see cref="LoggingPushSender"/>, <c>FcmPushSender</c>) ayrı ayrı yazılsaydı yarın eklenecek
/// üçüncü sağlayıcı ölçümü sessizce atlayabilirdi. Sarmalayıcı, kompozisyon noktasında bir kez
/// kurulur ve <b>hangi sağlayıcı seçilirse seçilsin</b> ölçüm çalışır.
/// </para>
/// </summary>
public sealed class MeteredPushSender(IPushSender inner, EgitimUssuMetrics metrics) : IPushSender
{
    /// <inheritdoc />
    public async Task<PushResult> SendAsync(
        string deviceToken,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await inner.SendAsync(deviceToken, message, cancellationToken);
            metrics.RecordPushSent(result.Status.ToString());
            return result;
        }
        catch (Exception) when (RecordFailure())
        {
            // `when` filtresi asla true dönmez: istisna yutulmaz, yalnız ölçülür ve yükselmeye devam eder.
            throw;
        }
    }

    private bool RecordFailure()
    {
        metrics.RecordPushSent(nameof(PushDeliveryStatus.TransientFailure));
        return false;
    }
}

/// <summary>
/// C-04: E-posta gönderim sonucunu ölçen sarmalayıcı. Gerekçesi <see cref="MeteredPushSender"/> ile aynı.
/// </summary>
public sealed class MeteredEmailSender(IEmailSender inner, EgitimUssuMetrics metrics) : IEmailSender
{
    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await inner.SendAsync(message, cancellationToken);
            metrics.RecordEmailSent(success: true);
        }
        catch (Exception) when (RecordFailure())
        {
            throw;
        }
    }

    private bool RecordFailure()
    {
        metrics.RecordEmailSent(success: false);
        return false;
    }
}
