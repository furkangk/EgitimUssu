using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Membership.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMembershipModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<MembershipDbContext>(configuration, "Membership", MembershipDbContext.SchemaName);

        // Sorgu/komut kayıtları P09 Task 2–3'te eklenecek; bu görev yalnız domain + şema kuruyor.
        return services;
    }
}
