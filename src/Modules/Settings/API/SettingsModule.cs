using EgitimUssu.Modules.Settings.Application;
using EgitimUssu.Modules.Settings.Infrastructure;
using EgitimUssu.Shared.Application;
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
}

/// <summary>
/// Öğrencinin çalışma verisini öğretmen/veli ile paylaşım tercihini taşır (Veli V-B).
/// </summary>
public sealed record SetStudySharingRequest(bool ShareWithTeacher, bool ShareWithParent);
