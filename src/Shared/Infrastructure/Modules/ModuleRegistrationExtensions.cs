using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Shared.Infrastructure.Modules;

public static class ModuleRegistrationExtensions
{
    public static IServiceCollection AddDiscoveredModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<Assembly> assemblies)
    {
        var modules = assemblies
            .SelectMany(assembly => assembly.ExportedTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(IModule).IsAssignableFrom(type))
            .Select(type => (IModule)Activator.CreateInstance(type)!)
            .OrderBy(module => module.Name)
            .ToArray();

        services.AddSingleton<IReadOnlyCollection<IModule>>(modules);

        foreach (var module in modules)
        {
            module.RegisterServices(services, configuration);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapDiscoveredModules(this IEndpointRouteBuilder endpoints)
    {
        var modules = endpoints.ServiceProvider.GetRequiredService<IReadOnlyCollection<IModule>>();

        foreach (var module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
