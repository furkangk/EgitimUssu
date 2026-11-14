using System.Security.Claims;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Middleware;

/// <summary>
/// Y4: İstemci IP'sine göre partition'lı, Redis destekli dağıtık rate limiting.
/// Politika istek yoluna göre seçilir; Redis erişilemezse fail-open (istek geçer).
/// </summary>
public sealed class DistributedRateLimitMiddleware(
    RequestDelegate next,
    IRateLimiter rateLimiter,
    IOptions<RateLimitOptions> options)
{
    private static readonly Error RateLimited = new("shared.rate_limited", "Too many requests. Please try again later.");

    public async Task InvokeAsync(HttpContext context)
    {
        var settings = options.Value;
        var selection = settings.Enabled ? ResolvePolicy(context, settings) : null;

        if (selection is not null)
        {
            var (policyName, rule) = selection.Value;
            var partitionKey = $"{policyName}:{ResolvePartitionId(context, policyName, settings.TrustedProxyHopCount)}";
            var allowed = await rateLimiter.TryAcquireAsync(partitionKey, rule.PermitLimit, rule.Window, context.RequestAborted);
            if (!allowed)
            {
                await WriteRateLimitedAsync(context);
                return;
            }
        }

        await next(context);
    }

    /// <summary>
    /// Yol tabanlı politika seçimi: kimlik bilgisi alan uçlar sıkı (IP), refresh ayrı ve gevşek (IP),
    /// oturum açmış kullanıcının kimlik uçları kullanıcı başına, mesaj gönderimi spam'e karşı, diğer
    /// API uçları varsayılan, gerisi limitsiz.
    /// </summary>
    internal static (string PolicyName, RateLimitRule Rule)? ResolvePolicy(HttpContext context, RateLimitOptions settings)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/identity", out var identityPath))
        {
            // P15 T7 / N-32: Eskiden bütün /api/identity/* tek IP kovasındaydı (10/dk): okul NAT'ı
            // arkasında /me ve /refresh birbirini kilitliyordu.
            if (identityPath.StartsWithSegments("/refresh"))
            {
                return ("refresh", settings.Refresh);
            }

            return IsCredentialPath(identityPath) ? ("auth", settings.Auth) : ("session", settings.Session);
        }

        if (IsSendMessage(context))
        {
            return ("messaging", settings.Messaging);
        }

        if (path.StartsWithSegments("/api"))
        {
            return ("default", settings.Default);
        }

        return null;
    }

    /// <summary>Kimlik bilgisi (parola, sıfırlama/doğrulama kodu) alan anonim uçlar.</summary>
    private static bool IsCredentialPath(PathString identityPath)
        => identityPath.StartsWithSegments("/login")
            || identityPath.StartsWithSegments("/register")
            || identityPath.StartsWithSegments("/password-reset")
            || identityPath.StartsWithSegments("/email-verification");

    /// <summary>
    /// M16-1: Yalnız <c>POST /api/messaging/conversations/{id}/messages</c> mesaj gönderimidir.
    /// Gelen kutusu okuma ya da okundu işaretleme bu sıkı kotaya girmez.
    /// </summary>
    internal static bool IsSendMessage(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method)
            || !context.Request.Path.StartsWithSegments("/api/messaging/conversations", out _, out var remaining))
        {
            return false;
        }

        // Kalan yol "/{conversationId}/messages" olmalı — üç parça: "", id, "messages".
        var segments = remaining.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return segments.Length == 2
            && segments[1].Equals("messages", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Partition anahtarı: mesajlaşma ve oturum (<c>session</c>) kotası <b>kullanıcı</b> başınadır
    /// (aynı ağdaki kullanıcılar birbirinin kotasını tüketmesin), diğer politikalar istemci IP'si başına.
    /// Kullanıcı yoksa IP'ye düşülür — kota yine uygulanır.
    /// </summary>
    internal static string ResolvePartitionId(HttpContext context, string policyName, int trustedProxyHopCount)
    {
        if (policyName is "messaging" or "session")
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return userId;
            }
        }

        return ResolveClientId(context, trustedProxyHopCount);
    }

    /// <summary>
    /// A-02: İstemci kimliğini <c>X-Forwarded-For</c>'un <b>sonundan</b> güvenilir proxy sayısı kadar
    /// geriden okur.
    /// <para>
    /// Başlığın tamamını istemci de yazabilir; güvenilebilecek olan yalnız <b>son</b> girdilerdir,
    /// çünkü önümüzdeki her proxy kendi gördüğü soket adresini listenin sonuna ekler. Eskiden ilk
    /// girdi alınıyordu: istemcinin <c>X-Forwarded-For: 1.2.3.4</c> göndermesi her istekte yeni bir
    /// partition açıyor, böylece kimlik uçlarındaki 10/dk sınırı tümüyle atlanabiliyordu.
    /// </para>
    /// <para>
    /// Liste beklenenden kısaysa (zincir sanılandan sığ ya da başlık uydurulmuş) başlık güvenilmez
    /// sayılır ve soket adresine düşülür — eksik başlık, sınırı kapatma gerekçesi değildir.
    /// </para>
    /// </summary>
    /// <param name="context">İstek bağlamı.</param>
    /// <param name="trustedProxyHopCount">Önümüzdeki güvenilir proxy sayısı; <c>0</c> ise başlık yok sayılır.</param>
    /// <returns>Partition anahtarı olarak kullanılacak istemci kimliği.</returns>
    internal static string ResolveClientId(HttpContext context, int trustedProxyHopCount)
        => ClientIpResolver.Resolve(context, trustedProxyHopCount);

    private static async Task WriteRateLimitedAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var result = ApiErrorHttpResults.FromError(context, StatusCodes.Status429TooManyRequests, RateLimited);
        await result.ExecuteAsync(context);
    }
}
