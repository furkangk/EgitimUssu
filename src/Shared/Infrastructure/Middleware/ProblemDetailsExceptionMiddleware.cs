using EgitimUssu.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Middleware;

public sealed class ProblemDetailsExceptionMiddleware(
    RequestDelegate next,
    ILogger<ProblemDetailsExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var result = ApiErrorHttpResults.FromError(
                context,
                StatusCodes.Status500InternalServerError,
                new("shared.unexpected", "The request could not be completed."));
            await result.ExecuteAsync(context);
        }
    }
}
