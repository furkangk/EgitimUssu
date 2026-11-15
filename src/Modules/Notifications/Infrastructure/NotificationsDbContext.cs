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

    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

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
        builder.HasIndex(entity => new { entity.LessonScheduleId, entity.OccurrenceStartUtc, entity.RecipientUserId }).IsUnique();
        builder.HasIndex(entity => new { entity.Status, entity.RemindAtUtc }).HasFilter("\"Status\" = 'Pending'");
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

/// <summary>M11-3: Rolden bağımsız in-app bildirim. Rozet sorgusu (UserId + okunmamış) indeksli.</summary>
internal sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("user_notifications");
        builder.HasKey(entity => entity.Id);
        // Id istemcide IIdGenerator ile üretiliyor; EF'in OnAdd sayması güncelleme senaryosunu bozar (Öğrenilenler #3).
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.UserId).IsRequired();
        builder.Property(entity => entity.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Body).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.Route).HasMaxLength(256);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.PushStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.PushAttempts).IsRequired();
        builder.Property(entity => entity.ExpiresOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.NextAttemptOnUtc, entity.CreatedOnUtc, entity.Id })
            .HasDatabaseName("IX_user_notifications_push_due").HasFilter("\"PushStatus\" = 'Pending'");
        builder.Ignore(entity => entity.IsRead);
        builder.HasIndex(entity => new { entity.UserId, entity.ReadOnUtc });
        builder.HasIndex(entity => new { entity.UserId, entity.CreatedOnUtc, entity.Id });
        builder.HasIndex(entity => new { entity.UserId, entity.CreatedOnUtc, entity.Id }, "IX_user_notifications_unread_keyset")
            .HasFilter("\"ReadOnUtc\" IS NULL");
        builder.HasIndex(entity => entity.PushedOnUtc);
    }
}
