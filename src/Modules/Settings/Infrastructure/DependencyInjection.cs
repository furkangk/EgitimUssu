using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Settings.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSettingsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SettingsDbContext>(configuration, "Settings", SettingsDbContext.SchemaName);
        return services;
    }
}
