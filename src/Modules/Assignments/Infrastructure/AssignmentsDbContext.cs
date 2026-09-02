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

    public DbSet<LessonResource> LessonResources => Set<LessonResource>();

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
        // P04: teslim dosyasi artik IFileStorage anahtariyla saklanir (public URL degil).
        builder.Property(entity => entity.AttachmentStorageKey).HasMaxLength(512);
        builder.Property(entity => entity.AttachmentFileName).HasMaxLength(256);
        builder.Property(entity => entity.AttachmentContentType).HasMaxLength(128);
        builder.Property(entity => entity.TeacherFeedback).HasMaxLength(2000);
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
        builder.Property(entity => entity.Visibility).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.HasIndex(entity => entity.LessonSessionId).IsUnique();
    }
}

/// <summary>M06-1: Ders kaynagi. Listeleme sorgusu (aktif + ogretmen/ogrenci) indeksli.</summary>
internal sealed class LessonResourceConfiguration : IEntityTypeConfiguration<LessonResource>
{
    public void Configure(EntityTypeBuilder<LessonResource> builder)
    {
        builder.ToTable("lesson_resources");
        builder.HasKey(entity => entity.Id);
        // Id istemcide IIdGenerator ile uretiliyor (Ogrenilenler #3).
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.TeacherUserId).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.StorageKey).HasMaxLength(512);
        builder.Property(entity => entity.FileName).HasMaxLength(256);
        builder.Property(entity => entity.ContentType).HasMaxLength(128);
        builder.Property(entity => entity.Url).HasMaxLength(1024);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.IsActive });
        builder.HasIndex(entity => new { entity.StudentId, entity.IsActive });
        builder.HasIndex(entity => entity.LessonSessionId);
    }
}
