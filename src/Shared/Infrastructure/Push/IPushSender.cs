namespace EgitimUssu.Shared.Infrastructure.Push;

/// <summary>
/// K-02: Sağlayıcı-agnostik push gönderimi. Uygulama kodu FCM'i bilmez; sağlayıcı
/// <c>Push:Provider</c> ile seçilir (<see cref="LoggingPushSender"/> / <c>FcmPushSender</c>).
/// </summary>
public interface IPushSender
{
    Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken = default);
}
