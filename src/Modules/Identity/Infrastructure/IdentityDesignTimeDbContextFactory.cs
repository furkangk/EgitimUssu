using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Identity.Infrastructure;

public sealed class IdentityDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<IdentityDbContext>
{
    protected override string Schema => IdentityDbContext.SchemaName;
}
