using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Study.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStudyModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<StudyDbContext>(configuration, "Study", StudyDbContext.SchemaName);
        return services;
    }
}
