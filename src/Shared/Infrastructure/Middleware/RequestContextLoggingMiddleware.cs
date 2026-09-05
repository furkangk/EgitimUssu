using System.Diagnostics;
using EgitimUssu.Shared.Infrastructure.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Middleware;

/// <summary>
/// C-04: İstek korelasyonu ve erişim log'u.
/// <para>
/// Log satırı hem <b>korelasyon kimliğini</b> (istemciden gelen ya da üretilen) hem de aktif
/// OpenTelemetry <b>trace kimliğini</b> taşır: log'dan trace'e, trace'ten log'a geçiş ancak ikisi
/// aynı satırda olduğunda mümkündür.
/// </para>
/// <para>
/// Sorgu dizesi <see cref="SensitiveDataMask"/>'ten geçirilir — doğrulama token'ı ya da e-posta
/// adresi sorgu parametresiyle geldiğinde düz metin olarak log'a düşmesin.
/// </para>
/// </summary>
public sealed class RequestContextLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestContextLoggingMiddleware> logger)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        var startedAt = DateTime.UtcNow;
        await next(context);
        var duration = DateTime.UtcNow - startedAt;

        // Gözlemlenebilirlik kapalıyken aktif Activity olmayabilir; o zaman alan boş geçer.
        var traceId = Activity.Current?.TraceId.ToString() ?? string.Empty;

        logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {DurationMs} ms with correlation {CorrelationId} trace {TraceId}",
            context.Request.Method,
            SensitiveDataMask.MaskQueryString(context.Request.Path + context.Request.QueryString),
            context.Response.StatusCode,
            duration.TotalMilliseconds,
            correlationId,
            traceId);
    }
}
