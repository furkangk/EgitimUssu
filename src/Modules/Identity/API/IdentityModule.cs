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

        group.MapPost("/register", RegisterAsync)
        .WithSummary("Yeni kullanıcı kaydı oluşturur");

        group.MapPost("/login", LoginAsync)
        .WithSummary("Kullanıcı girişi yapar");

        group.MapPost("/refresh", RefreshTokenAsync)
        .WithSummary("Erişim tokenını yeniler");

        group.MapPost("/password-reset/request", RequestPasswordResetAsync)
        .WithSummary("Şifre sıfırlama isteği başlatır");

        group.MapPost("/password-reset/confirm", ConfirmPasswordResetAsync)
        .WithSummary("Şifre sıfırlama işlemini onaylar");

        group.MapPost("/email-verification/request", RequestEmailVerificationAsync)
        .WithSummary("E-posta doğrulama isteği başlatır");

        group.MapPost("/email-verification/confirm", ConfirmEmailVerificationAsync)
        .WithSummary("E-posta doğrulama işlemini onaylar");

        authorizedGroup.MapPost("/logout", LogoutAsync)
        .WithSummary("Kullanıcı oturumunu kapatır");

        authorizedGroup.MapGet("/users/{userId:guid}", GetUserByIdAsync)
        .WithSummary("Kullanıcı bilgilerini getirir");
    }

    /// <summary>
    /// Yeni kullanıcı kaydı oluşturur.
    /// </summary>
    private static async Task<IResult> RegisterAsync(
        HttpContext context,
        RegisterUserRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// Kullanıcı girişi yapar.
    /// </summary>
    private static async Task<IResult> LoginAsync(
        HttpContext context,
        LoginUserRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new LoginUserCommand(request.Email, request.Password, request.DeviceName),
            cancellationToken);

        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Erişim tokenını yeniler.
    /// </summary>
    private static async Task<IResult> RefreshTokenAsync(
        HttpContext context,
        RefreshTokenRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new RefreshTokenCommand(request.RefreshToken, request.DeviceName), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Şifre sıfırlama isteği başlatır.
    /// </summary>
    private static async Task<IResult> RequestPasswordResetAsync(
        HttpContext context,
        PasswordResetRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new RequestPasswordResetCommand(request.Email), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Şifre sıfırlama işlemini onaylar.
    /// </summary>
    private static async Task<IResult> ConfirmPasswordResetAsync(
        HttpContext context,
        PasswordResetConfirmRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ResetPasswordCommand(request.Email, request.Token, request.NewPassword), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// E-posta doğrulama isteği başlatır.
    /// </summary>
    private static async Task<IResult> RequestEmailVerificationAsync(
        HttpContext context,
        EmailVerificationRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new RequestEmailVerificationCommand(request.Email), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// E-posta doğrulama işlemini onaylar.
    /// </summary>
    private static async Task<IResult> ConfirmEmailVerificationAsync(
        HttpContext context,
        EmailVerificationConfirmRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ConfirmEmailVerificationCommand(request.Email, request.Token), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Kullanıcı oturumunu kapatır.
    /// </summary>
    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        LogoutRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new LogoutCommand(request.RefreshToken), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Kullanıcı bilgilerini getirir.
    /// </summary>
    private static async Task<IResult> GetUserByIdAsync(
        HttpContext context,
        Guid userId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetUserByIdQuery(userId), cancellationToken);
        return ToHttpResult(context, result);
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

/// <summary>
/// Yeni kullanıcı oluşturmak için kimlik, iletişim ve rol bilgilerini taşır.
/// </summary>
public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    IReadOnlyCollection<UserRole> Roles);

/// <summary>
/// Kullanıcı girişi için e-posta, parola ve isteğe bağlı cihaz adını taşır.
/// </summary>
public sealed record LoginUserRequest(string Email, string Password, string? DeviceName);

/// <summary>
/// Süresi dolan erişim tokenını yenilemek için refresh token ve cihaz adını taşır.
/// </summary>
public sealed record RefreshTokenRequest(string RefreshToken, string? DeviceName);

/// <summary>
/// Kullanıcı oturumunu sonlandırmak için iptal edilecek refresh tokenı taşır.
/// </summary>
public sealed record LogoutRequest(string RefreshToken);

/// <summary>
/// Şifre sıfırlama akışını başlatmak için kullanıcının e-posta adresini taşır.
/// </summary>
public sealed record PasswordResetRequest(string Email);

/// <summary>
/// Şifre sıfırlama akışını tamamlamak için e-posta, doğrulama tokenı ve yeni parolayı taşır.
/// </summary>
public sealed record PasswordResetConfirmRequest(string Email, string Token, string NewPassword);

/// <summary>
/// E-posta doğrulama akışını başlatmak için kullanıcının e-posta adresini taşır.
/// </summary>
public sealed record EmailVerificationRequest(string Email);

/// <summary>
/// E-posta doğrulama akışını tamamlamak için e-posta ve doğrulama tokenını taşır.
/// </summary>
public sealed record EmailVerificationConfirmRequest(string Email, string Token);
