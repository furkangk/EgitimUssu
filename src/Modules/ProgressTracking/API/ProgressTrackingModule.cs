using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.ProgressTracking.API;

public sealed class ProgressTrackingModule : ModuleDefinition
{
    public override string Name => "ProgressTracking";

    public override string RoutePrefix => "/api/progress-tracking";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddProgressTrackingModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", () => TypedResults.Ok(new
        {
            module = Name,
            route = RoutePrefix,
            state = "placeholder"
        }));
    }
}


