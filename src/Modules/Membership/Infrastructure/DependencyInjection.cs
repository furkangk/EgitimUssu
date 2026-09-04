using EgitimUssu.Modules.Membership.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Membership.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMembershipModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<MembershipDbContext>(configuration, "Membership", MembershipDbContext.SchemaName);

        // Tek örnek iki sözleşmeyi karşılar: modüller-arası IEntitlementDirectory ve modül-içi IMembershipReader.
        services.AddScoped<EntitlementDirectory>();
        services.AddScoped<IEntitlementDirectory>(provider => provider.GetRequiredService<EntitlementDirectory>());
        services.AddScoped<IMembershipReader>(provider => provider.GetRequiredService<EntitlementDirectory>());

        services.AddScoped<IQueryHandler<GetMyMembershipQuery, Result<MembershipStatusResponse>>, GetMyMembershipQueryHandler>();
        services.AddScoped<IQueryAuthorizer<GetMyMembershipQuery>, GetMyMembershipQueryAuthorizer>();

        // Plan yönetimi, deneme ve abonelik komutları P09 Task 3'te eklenecek.
        return services;
    }
}
