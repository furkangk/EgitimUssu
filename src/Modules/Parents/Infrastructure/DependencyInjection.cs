using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Parents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddParentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ParentsDbContext>(configuration, "Parents", ParentsDbContext.SchemaName);
        return services;
    }
}
