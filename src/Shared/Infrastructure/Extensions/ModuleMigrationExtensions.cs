using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Extensions;

public static class ModuleMigrationExtensions
{
    public static async Task ApplyModuleMigrationsAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("ModuleMigrations");
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
            }
        }
