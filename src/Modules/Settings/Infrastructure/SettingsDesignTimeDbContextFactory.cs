using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Settings.Infrastructure;

public sealed class SettingsDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<SettingsDbContext>
{
    protected override string Schema => SettingsDbContext.SchemaName;
}
