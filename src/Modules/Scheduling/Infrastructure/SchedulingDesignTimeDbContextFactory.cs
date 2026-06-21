using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

public sealed class SchedulingDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<SchedulingDbContext>
{
    protected override string Schema => SchedulingDbContext.SchemaName;
}
