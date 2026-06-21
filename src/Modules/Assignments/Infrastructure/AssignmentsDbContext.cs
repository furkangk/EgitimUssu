using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

public sealed class AssignmentsDbContext : ModuleDbContext
{
    public const string SchemaName = "assignments";

    public AssignmentsDbContext(
        DbContextOptions<AssignmentsDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<Assignment> Assignments => Set<Assignment>();

    public DbSet<LessonNote> LessonNotes => Set<LessonNote>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Assignments";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssignmentsDbContext).Assembly);
    }
}

internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(2000);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.AttachmentUrl).HasMaxLength(512);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.StudentId, entity.Status });
        builder.HasIndex(entity => entity.LessonSessionId);
    }
}

internal sealed class LessonNoteConfiguration : IEntityTypeConfiguration<LessonNote>
{
    public void Configure(EntityTypeBuilder<LessonNote> builder)
    {
        builder.ToTable("lesson_notes");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Summary).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.CoveredTopics).HasMaxLength(2000);
        builder.Property(entity => entity.Recommendations).HasMaxLength(2000);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.HasIndex(entity => entity.LessonSessionId).IsUnique();
    }
}
