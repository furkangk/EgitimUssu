using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Reviews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReviewsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ReviewsDbContext>(configuration, "Reviews", ReviewsDbContext.SchemaName);
        return services;
    }
}
