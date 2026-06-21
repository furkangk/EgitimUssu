using EgitimUssu.Modules.Settings.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Settings.Infrastructure;

public sealed class SettingsDbContext : ModuleDbContext
{
    public const string SchemaName = "settings";

    public SettingsDbContext(
        DbContextOptions<SettingsDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<UserSetting> UserSettings => Set<UserSetting>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Settings";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SettingsDbContext).Assembly);
    }
}

internal sealed class UserSettingConfiguration : IEntityTypeConfiguration<UserSetting>
{
    public void Configure(EntityTypeBuilder<UserSetting> builder)
    {
        builder.ToTable("user_settings");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.PrivacyLevel).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.SessionTerminationPolicy).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.LastUpdatedOnUtc).IsRequired();
        builder.HasIndex(entity => entity.UserId).IsUnique();
    }
}
