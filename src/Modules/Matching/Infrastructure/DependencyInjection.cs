using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Matching.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMatchingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<MatchingDbContext>(configuration, "Matching", MatchingDbContext.SchemaName);
        return services;
    }
}
