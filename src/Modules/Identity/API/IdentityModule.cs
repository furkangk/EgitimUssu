using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Identity.API;

public sealed class IdentityModule : ModuleDefinition
{
    public override string Name => "Identity";

    public override string RoutePrefix => "/api/identity";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints).RequireRateLimiting("auth");
        var authorizedGroup = group.MapGroup(string.Empty).RequireAuthorization("AuthenticatedUser");

        group.MapPost("/register", async (
            HttpContext context,
            RegisterUserRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(
                new RegisterUserCommand(
                    request.Email,
                    request.Password,
                    request.FirstName,
                    request.LastName,
                    request.PhoneNumber,
                    request.Roles),
                cancellationToken);

            return ToHttpResult(context, result);
        });

        group.MapPost("/login", async (
            HttpContext context,
            LoginUserRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(
                new LoginUserCommand(request.Email, request.Password, request.DeviceName),
                cancellationToken);

            return ToHttpResult(context, result);
        });

        group.MapPost("/refresh", async (
            HttpContext context,
            RefreshTokenRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new RefreshTokenCommand(request.RefreshToken, request.DeviceName), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapPost("/password-reset/request", async (
            HttpContext context,
            PasswordResetRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new RequestPasswordResetCommand(request.Email), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapPost("/password-reset/confirm", async (
            HttpContext context,
            PasswordResetConfirmRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new ResetPasswordCommand(request.Email, request.Token, request.NewPassword), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapPost("/email-verification/request", async (
            HttpContext context,
            EmailVerificationRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new RequestEmailVerificationCommand(request.Email), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapPost("/email-verification/confirm", async (
            HttpContext context,
            EmailVerificationConfirmRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new ConfirmEmailVerificationCommand(request.Email, request.Token), cancellationToken);
            return ToHttpResult(context, result);
        });

        authorizedGroup.MapPost("/logout", async (
            HttpContext context,
            LogoutRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new LogoutCommand(request.RefreshToken), cancellationToken);
            return ToHttpResult(context, result);
        });

        authorizedGroup.MapGet("/users/{userId:guid}", async (
            HttpContext context,
            Guid userId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new GetUserByIdQuery(userId), cancellationToken);
            return ToHttpResult(context, result);
        });
    }

    private static IResult ToHttpResult<T>(HttpContext context, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return result.Error.Code switch
        {
            "identity.duplicate_email" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "identity.user_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "identity.invalid_refresh_token" => ApiErrorHttpResults.FromError(context, StatusCodes.Status401Unauthorized, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }

    private static IResult ToHttpResult(HttpContext context, Result result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok();
        }

        return result.Error.Code switch
        {
            "identity.invalid_refresh_token" => ApiErrorHttpResults.FromError(context, StatusCodes.Status401Unauthorized, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    IReadOnlyCollection<UserRole> Roles);

public sealed record LoginUserRequest(string Email, string Password, string? DeviceName);
public sealed record RefreshTokenRequest(string RefreshToken, string? DeviceName);
public sealed record LogoutRequest(string RefreshToken);
public sealed record PasswordResetRequest(string Email);
public sealed record PasswordResetConfirmRequest(string Email, string Token, string NewPassword);
public sealed record EmailVerificationRequest(string Email);
public sealed record EmailVerificationConfirmRequest(string Email, string Token);
