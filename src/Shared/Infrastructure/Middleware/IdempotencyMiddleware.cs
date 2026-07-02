using System.Security.Claims;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Http;

namespace EgitimUssu.Shared.Infrastructure.Middleware;

/// <summary>
/// Y4: <c>Idempotency-Key</c> header'ı taşıyan mutasyon (POST/PUT/PATCH/DELETE) isteklerinde
/// tekrarlı gönderimlerin güvenli tekrarını sağlar: aynı anahtarla tamamlanmış istek saklı yanıtı
/// döndürür; hâlâ işlenen istek 409 alır. Redis erişilemezse fail-open (normal işlenir).
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next, IIdempotencyStore store)
{
    private const string HeaderName = "Idempotency-Key";
    private static readonly Error InProgress = new("shared.idempotency_in_progress", "A request with this idempotency key is already being processed.");

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldApply(context) ||
            context.Request.Headers[HeaderName].FirstOrDefault() is not { Length: > 0 } idempotencyKey)
        {
            await next(context);
            return;
        }

        var storeKey = BuildStoreKey(context, idempotencyKey);
        var (outcome, storedResponse) = await store.TryBeginAsync(storeKey, context.RequestAborted);

        if (outcome == IdempotencyOutcome.Duplicate && storedResponse is not null)
        {
            await ReplayAsync(context, storedResponse);
            return;
        }

        if (outcome == IdempotencyOutcome.InProgress)
        {
            var conflict = ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, InProgress);
            await conflict.ExecuteAsync(context);
            return;
        }

        await ExecuteAndCaptureAsync(context, storeKey);
    }

    private async Task ExecuteAndCaptureAsync(HttpContext context, string storeKey)
    {
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        var bytes = buffer.ToArray();

        // Yalnız başarılı (2xx) yanıtlar önbelleklenir; geçici hatalar tekrar denenebilir kalır.
        if (context.Response.StatusCode is >= 200 and < 300)
        {
            var captured = new IdempotentResponse(context.Response.StatusCode, context.Response.ContentType, bytes);
            await store.CompleteAsync(storeKey, captured, context.RequestAborted);
        }

        await originalBody.WriteAsync(bytes, context.RequestAborted);
    }

    private static async Task ReplayAsync(HttpContext context, IdempotentResponse response)
    {
        context.Response.StatusCode = response.StatusCode;
        if (!string.IsNullOrEmpty(response.ContentType))
        {
            context.Response.ContentType = response.ContentType;
        }

        await context.Response.Body.WriteAsync(response.Body, context.RequestAborted);
    }

    private static bool ShouldApply(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            return false;
        }

        return HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method);
    }

    private static string BuildStoreKey(HttpContext context, string idempotencyKey)
    {
        var subject = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        return $"{subject}:{context.Request.Method}:{context.Request.Path}:{idempotencyKey}";
    }
}
