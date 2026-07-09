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

    public DbSet<TopicGoal> TopicGoals => Set<TopicGoal>();

    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

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

internal sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("processed_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
    }
}
