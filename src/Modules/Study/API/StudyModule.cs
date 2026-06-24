using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Study.API;

public sealed class StudyModule : ModuleDefinition
{
    public override string Name => "Study";

    public override string RoutePrefix => "/api/study";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddStudyModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", GetStatus)
        .WithSummary("Çalışma modül durumunu getirir");
    }

    /// <summary>
    /// Çalışma modülünün API host tarafından yüklendiğini doğrulamak için geçici durum bilgisini döndürür.
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


