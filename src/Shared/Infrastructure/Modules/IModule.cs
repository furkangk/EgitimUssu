using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Shared.Infrastructure.Modules;

public interface IModule : IModuleServiceInstaller, IModuleEndpointMapper
{
    string Name { get; }

    string RoutePrefix { get; }
}

public interface IModuleServiceInstaller
{
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
}

public interface IModuleEndpointMapper
{
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
