using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Parents.Infrastructure;

public sealed class ParentsDbContext : ModuleDbContext
{
    public const string SchemaName = "parents";

    public ParentsDbContext(
        DbContextOptions<ParentsDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<ParentProfile> ParentProfiles => Set<ParentProfile>();

    public DbSet<ParentChildLink> ParentChildLinks => Set<ParentChildLink>();

    public DbSet<ChildProgressSnapshot> ChildProgressSnapshots => Set<ChildProgressSnapshot>();

    public DbSet<KnownStudent> KnownStudents => Set<KnownStudent>();

    public DbSet<ProcessedIntegrationEvent> ProcessedIntegrationEvents => Set<ProcessedIntegrationEvent>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Parents";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ParentsDbContext).Assembly);
    }
}

internal sealed class ParentProfileConfiguration : IEntityTypeConfiguration<ParentProfile>
{
    public void Configure(EntityTypeBuilder<ParentProfile> builder)
    {
        builder.ToTable("parent_profiles");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.FullName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ContactPhone).HasMaxLength(32);
        builder.Property(entity => entity.ContactEmail).HasMaxLength(256);
        builder.Property(entity => entity.NotificationChannel).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(entity => entity.UserId).IsUnique();
    }
}

internal sealed class ParentChildLinkConfiguration : IEntityTypeConfiguration<ParentChildLink>
{
    public void Configure(EntityTypeBuilder<ParentChildLink> builder)
    {
        builder.ToTable("parent_child_links");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ChildDisplayName).HasMaxLength(200);
        builder.Property(entity => entity.Relationship).HasMaxLength(64);
        builder.Property(entity => entity.InviteCode).HasMaxLength(64);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(entity => new { entity.ParentUserId, entity.StudentId });
        builder.HasIndex(entity => entity.StudentId);
    }
}

internal sealed class ChildProgressSnapshotConfiguration : IEntityTypeConfiguration<ChildProgressSnapshot>
{
    public void Configure(EntityTypeBuilder<ChildProgressSnapshot> builder)
    {
        builder.ToTable("child_progress_snapshots");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Currency).HasMaxLength(8).IsRequired();
        builder.Property(entity => entity.ExpectedPaymentTotal).HasColumnType("numeric(18,2)");
        builder.Property(entity => entity.CollectedPaymentTotal).HasColumnType("numeric(18,2)");
        builder.Property(entity => entity.OutstandingPaymentTotal).HasColumnType("numeric(18,2)");
        builder.HasIndex(entity => entity.StudentId).IsUnique();
    }
}

internal sealed class KnownStudentConfiguration : IEntityTypeConfiguration<KnownStudent>
{
    public void Configure(EntityTypeBuilder<KnownStudent> builder)
    {
        builder.ToTable("known_students");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => entity.StudentId).IsUnique();
    }
}

internal sealed class ProcessedIntegrationEventConfiguration : IEntityTypeConfiguration<ProcessedIntegrationEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedIntegrationEvent> builder)
    {
        builder.ToTable("processed_integration_events");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.EventName).HasMaxLength(256).IsRequired();
    }
}
