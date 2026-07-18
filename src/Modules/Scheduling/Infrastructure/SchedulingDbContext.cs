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

    public DbSet<StudyScheduleEntry> StudyScheduleEntries => Set<StudyScheduleEntry>();

    public DbSet<TimeOffBlock> TimeOffBlocks => Set<TimeOffBlock>();

    public DbSet<LessonOccurrenceException> LessonOccurrenceExceptions => Set<LessonOccurrenceException>();

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
        builder.Property(entity => entity.MeetingUrl).HasMaxLength(512);
        builder.Property(entity => entity.RescheduleNote).HasMaxLength(500);
        builder.Property(entity => entity.CancellationReason).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.Notes).HasMaxLength(1000);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.UpdatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.StartAtUtc });
        builder.HasIndex(entity => new { entity.StudentId, entity.StartAtUtc });
    }
}

internal sealed class LessonOccurrenceExceptionConfiguration : IEntityTypeConfiguration<LessonOccurrenceException>
{
    public void Configure(EntityTypeBuilder<LessonOccurrenceException> builder)
    {
        builder.ToTable("lesson_occurrence_exceptions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Note).HasMaxLength(500);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.SeriesLessonScheduleId, entity.OriginalStartAtUtc });
    }
}

internal sealed class TimeOffBlockConfiguration : IEntityTypeConfiguration<TimeOffBlock>
{
    public void Configure(EntityTypeBuilder<TimeOffBlock> builder)
    {
        builder.ToTable("time_off_blocks");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.StartAtUtc).IsRequired();
        builder.Property(entity => entity.EndAtUtc).IsRequired();
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.StartAtUtc });
    }
}

internal sealed class StudyScheduleEntryConfiguration : IEntityTypeConfiguration<StudyScheduleEntry>
{
    public void Configure(EntityTypeBuilder<StudyScheduleEntry> builder)
    {
        builder.ToTable("study_schedule_entries");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Subject).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Topic).HasMaxLength(160);
        builder.Property(entity => entity.TimeZone).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.RecurrenceRule).HasMaxLength(256);
        builder.Property(entity => entity.ColorHex).HasMaxLength(16);
        builder.Property(entity => entity.Notes).HasMaxLength(1000);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.UpdatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.StudentId, entity.Status });
    }
}
