using System.Security.Claims;
using System.Security.Cryptography;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Http;

namespace EgitimUssu.Shared.Infrastructure.Middleware;

/// <summary>
/// Y4: <c>Idempotency-Key</c> header'ı taşıyan mutasyon (POST/PUT/PATCH/DELETE) isteklerinde
/// kimliği doğrulanmış kullanıcıya göre tekrarlı gönderimlerin güvenli tekrarını sağlar:
/// aynı anahtar ve gövdeyle tamamlanmış istek saklı yanıtı döndürür; hâlâ işlenen istek 409 alır.
/// Farklı gövdeyle aynı anahtarın kullanılması 422 döndürür.
/// Anonim istekler ve Identity uçları önbelleklenmez. Redis erişilemezse fail-open (normal işlenir).
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next, IIdempotencyStore store)
{
    private const string HeaderName = "Idempotency-Key";
    private static readonly Error InProgress = new("shared.idempotency_in_progress", "A request with this idempotency key is already being processed.");

    private static readonly Error KeyReused = new("shared.idempotency_key_reused", "Bu işlem anahtarı farklı bir istek gövdesiyle kullanılmış. Yeni işlem için yeni anahtar kullanın.");

    public async Task InvokeAsync(HttpContext context)
    {
        // An anonymous bucket would let unrelated login requests share access/refresh tokens.
        var subject = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        if (string.IsNullOrWhiteSpace(subject) || !ShouldApply(context) ||
            context.Request.Headers[HeaderName].FirstOrDefault() is not { Length: > 0 } idempotencyKey)
        {
            await next(context);
            return;
        }

        var requestBodyHash = await HashBodyAsync(context.Request, context.RequestAborted);
        var storeKey = BuildStoreKey(context, subject, idempotencyKey);
        var (outcome, storedResponse) = await store.TryBeginAsync(storeKey, requestBodyHash, context.RequestAborted);

        if (outcome == IdempotencyOutcome.BodyMismatch)
        {
            await ApiErrorHttpResults.FromError(context, StatusCodes.Status422UnprocessableEntity, KeyReused).ExecuteAsync(context);
            return;
        }

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

        await ExecuteAndCaptureAsync(context, storeKey, requestBodyHash);
    }

    private async Task ExecuteAndCaptureAsync(HttpContext context, string storeKey, string requestBodyHash)
    {
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        var completed = false;

        try
        {
            await next(context);

            var bytes = buffer.ToArray();

            // Yalnız başarılı (2xx) yanıtlar önbelleklenir; geçici hatalar tekrar denenebilir kalır.
            if (context.Response.StatusCode is >= 200 and < 300)
            {
                var captured = new IdempotentResponse(context.Response.StatusCode, context.Response.ContentType, bytes, requestBodyHash);
                await store.CompleteAsync(storeKey, captured, context.RequestAborted);
                completed = true;
            }

            context.Response.Body = originalBody;
            await originalBody.WriteAsync(bytes, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBody;

            // A-12: Başarısız istek (2xx dışı yanıt ya da istisna) anahtarı kilitli bırakmamalı.
            // Yorumdaki "geçici hatalar tekrar denenebilir kalır" niyeti eskiden yalnız TTL dolduğu
            // için gerçekleşiyordu: o ana kadar aynı anahtarla yapılan yeniden deneme 409 alıyordu.
            // `RequestAborted` bu noktada iptal edilmiş olabilir; serbest bırakma ona bağlanmamalı.
            if (!completed)
            {
                await store.ReleaseAsync(storeKey, CancellationToken.None);
            }
        }
    }

    private static async Task<string> HashBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        // Buffering spills large bodies to disk instead of keeping uploads entirely in memory.
        request.EnableBuffering();
        try
        {
            var hash = await SHA256.HashDataAsync(request.Body, cancellationToken);
            return Convert.ToHexString(hash);
        }
        finally
        {
            request.Body.Position = 0;
        }
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
        // Identity operations must always validate their current token/session state,
        // including when an authenticated client calls login or refresh.
        if (!context.Request.Path.StartsWithSegments("/api")
            || context.Request.Path.StartsWithSegments("/api/identity", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method);
    }

    private static string BuildStoreKey(HttpContext context, string subject, string idempotencyKey)
    {
        return $"{subject}:{context.Request.Method}:{context.Request.Path}:{idempotencyKey}";
    }
}
