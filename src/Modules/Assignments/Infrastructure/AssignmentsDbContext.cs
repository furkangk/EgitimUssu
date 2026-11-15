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
        builder.HasMany(entity => entity.SubmissionVersions).WithOne().HasForeignKey(entity => entity.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entity => entity.SubmissionVersions).HasField("_submissionVersions").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        builder.HasIndex(entity => new { entity.StudentId, entity.Status });
        builder.HasIndex(entity => entity.LessonSessionId);
        // M06-3: son teslim taramasi (AssignmentDueScanProcessor) bu indeksi kullanir.
        builder.HasIndex(entity => new { entity.Status, entity.DueDateUtc });
        // İki bağımsız kuyruk: yalnız hiç teslim edilmemiş ve ilgili uyarısı üretilmemiş satırlar.
        const string outstanding = "\"SubmittedOnUtc\" IS NULL AND \"Status\" IN ('Pending', 'ReturnedForRevision')";
        builder.HasIndex(entity => new { entity.DueDateUtc, entity.Id }, "IX_assignments_due_soon_scan")
            .HasFilter(outstanding + " AND \"DueSoonNotifiedOnUtc\" IS NULL AND \"DueDateUtc\" IS NOT NULL");
        builder.HasIndex(entity => new { entity.DueDateUtc, entity.Id }, "IX_assignments_missed_scan")
            .HasFilter(outstanding + " AND \"MissedNotifiedOnUtc\" IS NULL AND \"DueDateUtc\" IS NOT NULL");
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

internal sealed class AssignmentSubmissionVersionConfiguration : IEntityTypeConfiguration<AssignmentSubmissionVersion>
{
    public void Configure(EntityTypeBuilder<AssignmentSubmissionVersion> builder)
    {
        builder.ToTable("assignment_submission_versions");
        builder.HasKey(x => new { x.AssignmentId, x.Number });
        builder.Property(x => x.AssignmentId).ValueGeneratedNever();
        builder.Property(x => x.Number).ValueGeneratedNever();
        builder.Property(x => x.AttachmentUrl).HasMaxLength(512);
        builder.Property(x => x.StorageKey).HasMaxLength(512);
        builder.Property(x => x.FileName).HasMaxLength(256);
        builder.Property(x => x.ContentType).HasMaxLength(128);
        builder.Property(x => x.TeacherFeedback).HasMaxLength(2000);
    }
}
