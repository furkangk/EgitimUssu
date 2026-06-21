using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

public sealed class SchedulingDbContext : ModuleDbContext
{
    public const string SchemaName = "scheduling";

    public SchedulingDbContext(
        DbContextOptions<SchedulingDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<LessonSchedule> LessonSchedules => Set<LessonSchedule>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Scheduling";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulingDbContext).Assembly);
    }
}

internal sealed class LessonScheduleConfiguration : IEntityTypeConfiguration<LessonSchedule>
{
    public void Configure(EntityTypeBuilder<LessonSchedule> builder)
    {
        builder.ToTable("lesson_schedules");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Subject).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.LessonFormat).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.TimeZone).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.RecurrenceRule).HasMaxLength(256);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.LocationLabel).HasMaxLength(256);
        builder.Property(entity => entity.Notes).HasMaxLength(1000);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.UpdatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.StartAtUtc });
        builder.HasIndex(entity => new { entity.StudentId, entity.StartAtUtc });
    }
}
