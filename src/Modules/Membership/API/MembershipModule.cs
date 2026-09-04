using EgitimUssu.Modules.Membership.Infrastructure;
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

        // Plan listesi, abonelik ve entitlement uçları P09 Task 2–3'te eklenecek.
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
}
