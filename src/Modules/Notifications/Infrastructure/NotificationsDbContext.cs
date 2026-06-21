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
        builder.HasIndex(entity => entity.LessonScheduleId).IsUnique();
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.Status, entity.RemindAtUtc });
    }
}
