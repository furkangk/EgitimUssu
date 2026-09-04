using EgitimUssu.Modules.Membership.Application;
using EgitimUssu.Modules.Membership.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Membership.API;

public sealed class MembershipModule : ModuleDefinition
{
    public override string Name => "Membership";

    public override string RoutePrefix => "/api/membership";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMembershipModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", GetStatus)
            .WithSummary("Üyelik modül durumunu getirir");

        group.MapGet("/me", GetMyMembershipAsync)
            .WithSummary("Oturumdaki kullanıcının üyelik durumunu ve haklarını getirir")
            .RequireAuthorization("AuthenticatedUser");

        // Plan listesi, deneme ve abonelik uçları P09 Task 3'te eklenecek.
    }

    /// <summary>
    /// Üyelik modülünün API host tarafından yüklendiğini doğrulamak için geçici durum bilgisini döndürür.
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
    /// K-09: Oturumdaki kullanıcının etkin üyelik seviyesini, dönemini ve haklarını döner.
    /// İstemci (mobil paywall / reklam yerleşimi) bu ucu tek doğruluk kaynağı olarak okur.
    /// </summary>
    private static async Task<IResult> GetMyMembershipAsync(
        HttpContext context, IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetMyMembershipQuery(), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.Code switch
            {
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };
    }
}
