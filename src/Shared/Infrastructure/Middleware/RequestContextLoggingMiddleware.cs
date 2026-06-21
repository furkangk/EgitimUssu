using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Middleware;

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

        logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {DurationMs} ms with correlation {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            duration.TotalMilliseconds,
            correlationId);
    }
}
