using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

public sealed class ProgressTrackingDbContext : ModuleDbContext
{
    public const string SchemaName = "progress_tracking";

    public ProgressTrackingDbContext(
        DbContextOptions<ProgressTrackingDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<TopicMastery> TopicMasteries => Set<TopicMastery>();

    public DbSet<LessonAttendanceReceipt> LessonAttendanceReceipts => Set<LessonAttendanceReceipt>();

    public DbSet<TopicGoal> TopicGoals => Set<TopicGoal>();

    public DbSet<ProgressSnapshot> ProgressSnapshots => Set<ProgressSnapshot>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "ProgressTracking";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProgressTrackingDbContext).Assembly);
    }
}

internal sealed class TopicMasteryConfiguration : IEntityTypeConfiguration<TopicMastery>
{
    public void Configure(EntityTypeBuilder<TopicMastery> builder)
    {
        builder.ToTable("topic_masteries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Subject).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Topic).HasMaxLength(160).IsRequired();
        builder.Property(e => e.MasteryLevel).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.Trend).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.MasteryScore).HasPrecision(6, 2);
        builder.Property(e => e.AverageNetRatio).HasPrecision(6, 4);
        builder.Property(e => e.NetRatioSum).HasPrecision(10, 4);
        builder.Property(e => e.RecentNetRatio).HasPrecision(6, 4);
        builder.Property(e => e.PriorNetRatio).HasPrecision(6, 4);
        builder.HasIndex(e => new { e.StudentId, e.Subject, e.Topic }).IsUnique();
        builder.HasIndex(e => new { e.StudentId, e.IsWeakSpot });
    }
}

internal sealed class TopicGoalConfiguration : IEntityTypeConfiguration<TopicGoal>
{
    public void Configure(EntityTypeBuilder<TopicGoal> builder)
    {
        builder.ToTable("topic_goals");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Subject).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Topic).HasMaxLength(160).IsRequired();
        builder.Property(e => e.TargetMasteryLevel).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.TargetNetRatio).HasPrecision(6, 4);
        builder.Property(e => e.SetByRole).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(e => new { e.StudentId, e.Status });
    }
}

internal sealed class ProgressSnapshotConfiguration : IEntityTypeConfiguration<ProgressSnapshot>
{
    public void Configure(EntityTypeBuilder<ProgressSnapshot> builder)
    {
        builder.ToTable("progress_snapshots");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Subject).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Period).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.AverageNet).HasPrecision(6, 4);

        // Aynı öğrenci-ders-dönem için ikinci bir anlık görüntü yazılamaz (üretim turunun idempotency guard'ı).
        builder.HasIndex(e => new { e.StudentId, e.Subject, e.Period, e.PeriodStart }).IsUnique();
        builder.HasIndex(e => new { e.StudentId, e.Period, e.PeriodStart });
    }
}

internal sealed class LessonAttendanceReceiptConfiguration : IEntityTypeConfiguration<LessonAttendanceReceipt>
{
    public void Configure(EntityTypeBuilder<LessonAttendanceReceipt> builder)
    {
        builder.ToTable("lesson_attendance_receipts");
        builder.HasKey(item => item.LessonSessionId);
        builder.Property(item => item.LessonSessionId).ValueGeneratedNever();
    }
}
