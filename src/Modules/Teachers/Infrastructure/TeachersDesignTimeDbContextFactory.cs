using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Teachers.Infrastructure;

public sealed class TeachersDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<TeachersDbContext>
{
    protected override string Schema => TeachersDbContext.SchemaName;
}
