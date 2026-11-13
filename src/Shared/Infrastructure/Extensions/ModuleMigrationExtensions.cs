using EgitimUssu.Shared.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Extensions;

public static class ModuleMigrationExtensions
{
    /// <summary>
    /// Keşfedilen tüm modül <c>DbContext</c>'lerinin migration'larını uygular.
    /// <para>
    /// E-05: "Koşmalı mı?" kararı <b>buradadır</b>, çağıranda değil. Bayrak hem burada hem
    /// <c>Program.cs</c>'te sınansaydı kural iki yere dağılır ve biri unutulduğunda üretimde
    /// gözetimsiz şema değişikliği koşabilirdi. Karar <see cref="MigrationStartupGuard"/>'ın.
    /// </para>
    /// </summary>
    /// <returns>Migration'lar uygulandıysa <c>true</c>; guard engellediyse <c>false</c>.</returns>
    public static async Task<bool> ApplyModuleMigrationsAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("ModuleMigrations");

        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        var blockReason = MigrationStartupGuard.BlockReason(
            environment.EnvironmentName, options.ApplyMigrationsOnStartup);
        if (blockReason is not null)
        {
            // Bayrak açıkken engellenmek beklenti ile gerçeğin ayrıldığı yerdir; sessizce geçilmez.
            if (options.ApplyMigrationsOnStartup)
            {
                logger.LogWarning("Açılışta migration atlandı: {Reason}", blockReason);
            }

            return false;
        }

        var descriptors = scope.ServiceProvider
            .GetServices<Persistence.ModuleDbContextDescriptor>()
            .OrderBy(item => item.ModuleName)
            .ToArray();

        foreach (var descriptor in descriptors)
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(descriptor.DbContextType);
            logger.LogInformation("Applying migrations for module {Module}", descriptor.ModuleName);
            await context.Database.MigrateAsync(cancellationToken);
        }

        return true;
    }
}
