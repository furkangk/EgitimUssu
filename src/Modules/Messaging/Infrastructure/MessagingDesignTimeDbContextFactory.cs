using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Messaging.Infrastructure;

public sealed class MessagingDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<MessagingDbContext>
{
    protected override string Schema => MessagingDbContext.SchemaName;
}
