using EgitimUssu.Modules.Settings.Infrastructure;
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
}


