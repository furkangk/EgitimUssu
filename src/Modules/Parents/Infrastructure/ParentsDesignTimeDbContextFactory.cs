using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Parents.Infrastructure;

public sealed class ParentsDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<ParentsDbContext>
{
    protected override string Schema => ParentsDbContext.SchemaName;
}
