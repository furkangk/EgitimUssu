using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

public sealed class NotificationsDbContext : ModuleDbContext
{
    public const string SchemaName = "notifications";

    public NotificationsDbContext(
        DbContextOptions<NotificationsDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<LessonReminder> LessonReminders => Set<LessonReminder>();

    public DbSet<ParentNotification> ParentNotifications => Set<ParentNotification>();

    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    public DbSet<ProcessedIntegrationEvent> ProcessedIntegrationEvents => Set<ProcessedIntegrationEvent>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Notifications";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}

internal sealed class LessonReminderConfiguration : IEntityTypeConfiguration<LessonReminder>
{
    public void Configure(EntityTypeBuilder<LessonReminder> builder)
    {
        builder.ToTable("lesson_reminders");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Message).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.Channel).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        // P03 Task 3: push teslimat izleme (başarısız denemeler + son hata).
        builder.Property(entity => entity.AttemptCount).IsRequired();
        builder.Property(entity => entity.LastError).HasMaxLength(1000);
        builder.HasIndex(entity => entity.LessonScheduleId).IsUnique();
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.Status, entity.RemindAtUtc });
    }
}

internal sealed class ParentNotificationConfiguration : IEntityTypeConfiguration<ParentNotification>
{
    public void Configure(EntityTypeBuilder<ParentNotification> builder)
    {
        builder.ToTable("parent_notifications");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Message).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.ParentUserId, entity.CreatedOnUtc });
    }
}

internal sealed class ProcessedIntegrationEventConfiguration : IEntityTypeConfiguration<ProcessedIntegrationEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedIntegrationEvent> builder)
    {
        builder.ToTable("processed_integration_events");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.EventName).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.ProcessedOnUtc).IsRequired();
    }
}

internal sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_tokens");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.Token).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.Platform).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.DeviceName).HasMaxLength(128);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.LastSeenOnUtc).IsRequired();
        builder.HasIndex(entity => entity.Token).IsUnique();
        builder.HasIndex(entity => new { entity.UserId, entity.IsActive });
    }
}
