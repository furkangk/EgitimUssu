using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

public sealed class AssignmentsDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<AssignmentsDbContext>
{
    protected override string Schema => AssignmentsDbContext.SchemaName;
}
