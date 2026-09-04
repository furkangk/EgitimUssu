using EgitimUssu.Modules.Messaging.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Messaging.API;

public sealed class MessagingModule : ModuleDefinition
{
    public override string Name => "Messaging";

    public override string RoutePrefix => "/api/messaging";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMessagingModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", GetStatus)
            .WithSummary("Mesajlaşma modül durumunu getirir");
    }

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
