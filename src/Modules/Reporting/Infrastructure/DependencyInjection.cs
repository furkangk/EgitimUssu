using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ReportingDbContext>(configuration, "Reporting", ReportingDbContext.SchemaName);
        return services;
    }
}
