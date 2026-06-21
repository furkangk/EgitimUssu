using System.Collections.Generic;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Http;

namespace EgitimUssu.Shared.Infrastructure.Http;

public static class ApiErrorHttpResults
{
    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors =
        new Dictionary<string, string[]>();

    public static IResult FromError(HttpContext context, int status, Error error)
    {
        var payload = new ApiErrorResponse(
            Code: string.IsNullOrWhiteSpace(error.Code) ? "shared.error" : error.Code,
            Message: string.IsNullOrWhiteSpace(error.Message) ? "Request failed." : error.Message,
            Errors: error.Errors ?? EmptyErrors,
            Status: status,
            TraceId: context.TraceIdentifier);

        return Results.Json(payload, statusCode: status);
    }

    public static IResult Forbidden(HttpContext context, string message = "Forbidden.")
    {
        return FromError(
            context,
            StatusCodes.Status403Forbidden,
            new Error("shared.forbidden", message));
    }

    public static IResult Unauthorized(HttpContext context, string message = "Unauthorized.")
    {
        return FromError(
            context,
            StatusCodes.Status401Unauthorized,
            new Error("shared.unauthorized", message));
    }
}

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]> Errors,
    int Status,
    string TraceId);
