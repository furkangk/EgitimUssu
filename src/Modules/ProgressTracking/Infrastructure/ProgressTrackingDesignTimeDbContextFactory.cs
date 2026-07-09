using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

public sealed class ProgressTrackingDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<ProgressTrackingDbContext>
{
    protected override string Schema => ProgressTrackingDbContext.SchemaName;
}
