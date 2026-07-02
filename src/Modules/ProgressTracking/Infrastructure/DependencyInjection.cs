using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProgressTrackingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // K4: Domain modeli/entity yok — boş DbContext kaydı outbox taramasını çökertir. Modül
        // gerçek bir modele kavuştuğunda AddModuleDbContext + migration yeniden eklenmelidir.
        return services;
    }
}
