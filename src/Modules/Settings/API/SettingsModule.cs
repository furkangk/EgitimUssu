using EgitimUssu.Modules.Settings.Application;
using EgitimUssu.Modules.Settings.Domain;
using EgitimUssu.Modules.Settings.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Settings.API;

public sealed class SettingsModule : ModuleDefinition
{
    public override string Name => "Settings";

    public override string RoutePrefix => "/api/settings";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSettingsModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", GetStatus)
        .WithSummary("Ayarlar modül durumunu getirir");

        group.MapPut("/users/{userId:guid}/study-sharing", SetStudySharingAsync)
            .WithSummary("Öğrencinin bireysel çalışma verisini öğretmen/veli ile paylaşımını ayarlar")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/users/{userId:guid}", GetUserSettingsAsync)
            .WithSummary("Kullanıcının ayarlarını getirir (kayıt yoksa varsayılan)")
            .RequireAuthorization("AuthenticatedUser");

        group.MapPut("/users/{userId:guid}", UpdateUserSettingsAsync)
            .WithSummary("Bildirim/gizlilik/güvenlik tercihlerini günceller")
            .RequireAuthorization("AuthenticatedUser");
    }

    /// <summary>
    /// Ayarlar modülünün API host tarafından yüklendiğini doğrulamak için geçici durum bilgisini döndürür.
    /// </summary>
    private IResult GetStatus()
    {
        return TypedResults.Ok(new
        {
            module = Name,
            route = RoutePrefix,
            state = "placeholder"
        });
    }

    /// <summary>
    /// Öğrencinin bireysel çalışma verisini öğretmen ve/veya veli ile paylaşımını ayarlar (upsert).
    /// </summary>
    private static async Task<IResult> SetStudySharingAsync(
        HttpContext context, Guid userId, SetStudySharingRequest request,
        ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new SetStudySharingCommand(userId, request.ShareWithTeacher, request.ShareWithParent),
            cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.Code switch
            {
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };
    }

    /// <summary>
    /// Kullanıcının ayarlarını getirir; kayıt yoksa varsayılan ayar nesnesi döner.
    /// </summary>
    private static async Task<IResult> GetUserSettingsAsync(
        HttpContext context, Guid userId,
        IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch<Result<UserSettingsResponse>>(
            new GetUserSettingsQuery(userId), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.Code switch
            {
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };
    }

    /// <summary>
    /// Kullanıcının bildirim/gizlilik/güvenlik tercihlerini günceller (upsert).
    /// </summary>
    private static async Task<IResult> UpdateUserSettingsAsync(
        HttpContext context, Guid userId, UpdateUserSettingsRequest request,
        ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new UpdateUserSettingsCommand(
                userId,
                request.PushNotificationsEnabled,
                request.EmailNotificationsEnabled,
                request.UpcomingLessonReminderEnabled,
                request.HomeworkReminderEnabled,
                request.PaymentReminderEnabled,
                request.WeeklySummaryEnabled,
                request.PrivacyLevel,
                request.SessionTerminationPolicy),
            cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.Code switch
            {
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };
    }
}

/// <summary>
/// Bildirim/gizlilik/güvenlik tercihlerinin güncelleme isteği (M15-1).
/// </summary>
/// <param name="PushNotificationsEnabled">Anlık bildirimler açık mı.</param>
/// <param name="EmailNotificationsEnabled">E-posta bildirimleri açık mı.</param>
/// <param name="UpcomingLessonReminderEnabled">Yaklaşan ders hatırlatması açık mı.</param>
/// <param name="HomeworkReminderEnabled">Ödev hatırlatması açık mı.</param>
/// <param name="PaymentReminderEnabled">Ödeme hatırlatması açık mı.</param>
/// <param name="WeeklySummaryEnabled">Haftalık özet açık mı.</param>
/// <param name="PrivacyLevel">Çalışma verisi gizlilik seviyesi.</param>
/// <param name="SessionTerminationPolicy">Girişte diğer oturumlara ne olacağı.</param>
public sealed record UpdateUserSettingsRequest(
    bool PushNotificationsEnabled,
    bool EmailNotificationsEnabled,
    bool UpcomingLessonReminderEnabled,
    bool HomeworkReminderEnabled,
    bool PaymentReminderEnabled,
    bool WeeklySummaryEnabled,
    PrivacyLevel PrivacyLevel,
    SessionTerminationPolicy SessionTerminationPolicy);

/// <summary>
/// Öğrencinin çalışma verisini öğretmen/veli ile paylaşım tercihini taşır (Veli V-B).
/// </summary>
public sealed record SetStudySharingRequest(bool ShareWithTeacher, bool ShareWithParent);
