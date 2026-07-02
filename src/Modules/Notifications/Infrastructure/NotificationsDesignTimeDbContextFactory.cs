using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

public sealed class NotificationsDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<NotificationsDbContext>
{
    protected override string Schema => NotificationsDbContext.SchemaName;
}
