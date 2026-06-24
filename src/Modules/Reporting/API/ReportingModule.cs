using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Reporting.API;

public sealed class ReportingModule : ModuleDefinition
{
    public override string Name => "Reporting";

    public override string RoutePrefix => "/api/reporting";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddReportingModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", GetStatus)
        .WithSummary("Raporlama modül durumunu getirir");
    }

    /// <summary>
    /// Raporlama modülünün API host tarafından yüklendiğini doğrulamak için geçici durum bilgisini döndürür.
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


