using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Membership.Infrastructure;

public sealed class MembershipDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<MembershipDbContext>
{
    protected override string Schema => MembershipDbContext.SchemaName;
}
