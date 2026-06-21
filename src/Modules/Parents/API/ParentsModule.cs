using EgitimUssu.Modules.Parents.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Parents.API;

public sealed class ParentsModule : ModuleDefinition
{
    public override string Name => "Parents";

    public override string RoutePrefix => "/api/parents";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddParentsModule(configuration);
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


