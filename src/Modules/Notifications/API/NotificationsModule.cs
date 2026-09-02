using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Notifications.API;

public sealed class NotificationsModule : ModuleDefinition
{
    public override string Name => "Notifications";

    public override string RoutePrefix => "/api/notifications";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNotificationsModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapGet("/teachers/{teacherUserId:guid}/lesson-reminders", ListTeacherLessonRemindersAsync)
        .WithSummary("Öğretmenin ders hatırlatmalarını listeler");

        group.MapGet("/parents/{parentUserId:guid}/notifications", ListParentNotificationsAsync)
        .WithSummary("Velinin bildirimlerini listeler (Veli V-E)");

        group.MapPost("/device-tokens", RegisterDeviceTokenAsync)
        .WithSummary("Cihaz push token'ını kaydeder/tazeler (M11-2)");

        group.MapDelete("/device-tokens/{token}", UnregisterDeviceTokenAsync)
        .WithSummary("Cihaz push token'ını devre dışı bırakır (çıkış)");
    }

    /// <summary>
    /// Cihazın push token'ını kaydeder; token zaten varsa yeni satır açmadan tazeler (M11-2).
    /// </summary>
    private static async Task<IResult> RegisterDeviceTokenAsync(
        HttpContext context,
        RegisterDeviceTokenRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new RegisterDeviceTokenCommand(request.UserId, request.Token, request.Platform, request.DeviceName),
            cancellationToken);

        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Cihazın push token'ını devre dışı bırakır (çıkış akışı). Bilinmeyen token da başarı döner.
    /// </summary>
    private static async Task<IResult> UnregisterDeviceTokenAsync(
        HttpContext context,
        string token,
        ICurrentUser currentUser,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        // Kullanıcı yalnız kendi cihaz token'ını siler; sahiplik gövdeden değil token'dan okunur.
        if (!Guid.TryParse(currentUser.UserId, out var userId))
        {
            return ApiErrorHttpResults.Forbidden(context, "Bu işlemi yapma yetkiniz yok.");
        }

        var result = await dispatcher.Dispatch(new UnregisterDeviceTokenCommand(userId, token), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Velinin bildirimlerini (en yeni önce) listeler (Veli V-E). Yalnız Premium veliye bildirim üretilir.
    /// </summary>
    private static async Task<IResult> ListParentNotificationsAsync(
        HttpContext context,
        Guid parentUserId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ListParentNotificationsQuery(parentUserId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmenin ders planlarından üretilen hatırlatma kayıtlarını listeler; istenirse yalnızca aktif olanları döndürür.
    /// </summary>
    private static async Task<IResult> ListTeacherLessonRemindersAsync(
        HttpContext context,
        Guid teacherUserId,
        bool activeOnly,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ListTeacherLessonRemindersQuery(teacherUserId, activeOnly), cancellationToken);
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
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

/// <summary>Cihaz push token kayıt isteği (M11-2).</summary>
public sealed record RegisterDeviceTokenRequest(Guid UserId, string Token, DevicePlatform Platform, string? DeviceName);
