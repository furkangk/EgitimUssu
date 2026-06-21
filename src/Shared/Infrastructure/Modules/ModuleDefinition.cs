using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Shared.Infrastructure.Modules;

public abstract class ModuleDefinition : IModule
{
    public abstract string Name { get; }

    public abstract string RoutePrefix { get; }

    public abstract void RegisterServices(IServiceCollection services, IConfiguration configuration);

    public abstract void MapEndpoints(IEndpointRouteBuilder endpoints);

    protected RouteGroupBuilder CreateModuleGroup(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGroup(RoutePrefix);
    }
}
