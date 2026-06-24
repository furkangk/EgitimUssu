using EgitimUssu.Modules.Reviews.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Reviews.API;

public sealed class ReviewsModule : ModuleDefinition
{
    public override string Name => "Reviews";

    public override string RoutePrefix => "/api/reviews";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddReviewsModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", GetStatus)
        .WithSummary("Yorumlar modül durumunu getirir");
    }

    /// <summary>
    /// Yorumlar modülünün API host tarafından yüklendiğini doğrulamak için geçici durum bilgisini döndürür.
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


