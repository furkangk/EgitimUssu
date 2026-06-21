using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProgressTrackingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ProgressTrackingDbContext>(configuration, "ProgressTracking", ProgressTrackingDbContext.SchemaName);
        return services;
    }
}
