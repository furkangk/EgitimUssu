using EgitimUssu.Modules.Matching.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Matching.API;

public sealed class MatchingModule : ModuleDefinition
{
    public override string Name => "Matching";

    public override string RoutePrefix => "/api/matching";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMatchingModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", GetStatus)
        .WithSummary("Eşleştirme modül durumunu getirir");
    }

    /// <summary>
    /// Eşleştirme modülünün API host tarafından yüklendiğini doğrulamak için geçici durum bilgisini döndürür.
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


