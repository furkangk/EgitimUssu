using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Matching.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMatchingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // K4: Bu modülün henüz domain modeli/entity'si yok. Boş bir DbContext kaydı, outbox
        // tablosu hiç migrate edilmediğinden prod'da outbox işlemesini (ProcessPendingAsync) çökertir.
        // Modül bir domain modeli kazandığında AddModuleDbContext + migration yeniden eklenmelidir.
        return services;
    }
}
