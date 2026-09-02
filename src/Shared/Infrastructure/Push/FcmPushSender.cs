using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EgitimUssu.Shared.Infrastructure.Configuration;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Push;

/// <summary>Service-account ile FCM erişim token'ı üretir (Google kütüphanesi 1 saatlik token'ı kendi önbellekler).</summary>
public interface IFcmAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// <see cref="PushOptions.ServiceAccountJson"/> (ortam değişkeni) ya da
/// <see cref="PushOptions.ServiceAccountJsonPath"/> (mount edilen dosya) üzerinden kimlik kurar.
/// </summary>
public sealed class GoogleFcmAccessTokenProvider : IFcmAccessTokenProvider
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly PushOptions _options;
    private GoogleCredential? _credential;

    public GoogleFcmAccessTokenProvider(IOptions<PushOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        // Not: GoogleCredential.FromFile/FromJson 1.76'da [Obsolete]; CredentialFactory önerilen yol.
        _credential ??= (string.IsNullOrWhiteSpace(_options.ServiceAccountJson)
                ? CredentialFactory.FromFile<ServiceAccountCredential>(_options.ServiceAccountJsonPath)
                : CredentialFactory.FromJson<ServiceAccountCredential>(_options.ServiceAccountJson))
            .ToGoogleCredential()
            .CreateScoped(Scope);

        return await _credential.UnderlyingCredential
            .GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
    }
}

/// <summary>
/// FCM HTTP v1 üzerinden push gönderir; Android ve iOS (APNs proxy) tek kanaldan (karar K-02).
/// Üç sonuç ayrımı çağıranın davranışını belirler: <see cref="PushDeliveryStatus.TokenInvalid"/>
/// token'ı kapatır, <see cref="PushDeliveryStatus.TransientFailure"/> yeniden denemeye bırakır.
/// </summary>
public sealed class FcmPushSender : IPushSender
{
    private readonly HttpClient _httpClient;
    private readonly IFcmAccessTokenProvider _tokenProvider;
    private readonly PushOptions _options;
    private readonly ILogger<FcmPushSender> _logger;

    public FcmPushSender(
        HttpClient httpClient,
        IFcmAccessTokenProvider tokenProvider,
        IOptions<PushOptions> options,
        ILogger<FcmPushSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PushResult> SendAsync(
        string deviceToken,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        var url = $"https://fcm.googleapis.com/v1/projects/{_options.ProjectId}/messages:send";

        var payload = new
        {
            message = new
            {
                token = deviceToken,
                notification = new { title = message.Title, body = message.Body },
                data = message.Data,
                android = new { priority = "high" },
                apns = new { headers = new Dictionary<string, string> { ["apns-priority"] = "10" } }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new PushResult(PushDeliveryStatus.Delivered, null);
        }

        if (IsTokenInvalid(response.StatusCode, body))
        {
            _logger.LogInformation("FCM token geçersiz, devre dışı bırakılacak: {Body}", body);
            return new PushResult(PushDeliveryStatus.TokenInvalid, body);
        }

        _logger.LogWarning("FCM gönderimi başarısız ({Status}): {Body}", response.StatusCode, body);
        return new PushResult(PushDeliveryStatus.TransientFailure, body);
    }

    private static bool IsTokenInvalid(HttpStatusCode statusCode, string body)
    {
        if (statusCode is not (HttpStatusCode.NotFound or HttpStatusCode.BadRequest))
        {
            return false;
        }

        return body.Contains("UNREGISTERED", StringComparison.Ordinal)
            || body.Contains("INVALID_ARGUMENT", StringComparison.Ordinal);
    }
}
