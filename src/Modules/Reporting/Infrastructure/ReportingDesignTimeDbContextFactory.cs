using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

public sealed class ReportingDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<ReportingDbContext>
{
    protected override string Schema => ReportingDbContext.SchemaName;
}
