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
            var partitionKey = $"{policyName}:{ResolvePartitionId(context, policyName)}";
            var allowed = await rateLimiter.TryAcquireAsync(partitionKey, rule.PermitLimit, rule.Window, context.RequestAborted);
            if (!allowed)
            {
                await WriteRateLimitedAsync(context);
                return;
            }
        }

        await next(context);
    }

    /// <summary>Yol tabanlı politika seçimi: kimlik uçları sıkı, mesaj gönderimi spam'e karşı, diğer API uçları varsayılan, gerisi limitsiz.</summary>
    internal static (string PolicyName, RateLimitRule Rule)? ResolvePolicy(HttpContext context, RateLimitOptions settings)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/identity"))
        {
            return ("auth", settings.Auth);
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
    /// Partition anahtarı: mesajlaşma kotası <b>kullanıcı</b> başınadır (aynı ağdaki kullanıcılar
    /// birbirinin kotasını tüketmesin), diğer politikalar istemci IP'si başına.
    /// </summary>
    internal static string ResolvePartitionId(HttpContext context, string policyName)
    {
        if (policyName == "messaging")
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return userId;
            }
        }

        return ResolveClientId(context);
    }

    /// <summary>Ters-proxy arkasında (Render) gerçek istemci IP'si <c>X-Forwarded-For</c>'un ilk girdisidir.</summary>
    internal static string ResolveClientId(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();
            if (first.Length > 0)
            {
                return first;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

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
