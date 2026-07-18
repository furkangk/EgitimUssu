using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Teachers.Infrastructure;

public sealed class TeachersDbContext : ModuleDbContext
{
    public const string SchemaName = "teachers";

    public TeachersDbContext(
        DbContextOptions<TeachersDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();

    public DbSet<TeacherAvailabilitySlot> TeacherAvailabilitySlots => Set<TeacherAvailabilitySlot>();

    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();

    public DbSet<TeacherCertificate> TeacherCertificates => Set<TeacherCertificate>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Teachers";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TeachersDbContext).Assembly);
    }
}

internal sealed class TeacherProfileConfiguration : IEntityTypeConfiguration<TeacherProfile>
{
    public void Configure(EntityTypeBuilder<TeacherProfile> builder)
    {
        builder.ToTable("teacher_profiles");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.FullName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Subject).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.City).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.District).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Biography).HasMaxLength(2000);
        builder.Property(entity => entity.Headline).HasMaxLength(180);
        builder.Property(entity => entity.LessonFormat).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.EducationLevel).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.HourlyRateAmount).HasPrecision(18, 2);
        builder.Property(entity => entity.Currency).HasMaxLength(8).IsRequired();
        builder.Property(entity => entity.ProfilePhotoUrl).HasMaxLength(512);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.UpdatedOnUtc).IsRequired();
        builder.HasIndex(entity => entity.UserId).IsUnique();
        builder.HasIndex(entity => new { entity.City, entity.Subject });
        builder.HasMany(entity => entity.AvailabilitySlots).WithOne().HasForeignKey(entity => entity.TeacherProfileId);
        builder.HasMany(entity => entity.Subjects).WithOne().HasForeignKey(entity => entity.TeacherProfileId);
        builder.HasMany(entity => entity.Certificates).WithOne().HasForeignKey(entity => entity.TeacherProfileId);
    }
}

internal sealed class TeacherSubjectConfiguration : IEntityTypeConfiguration<TeacherSubject>
{
    public void Configure(EntityTypeBuilder<TeacherSubject> builder)
    {
        builder.ToTable("teacher_subjects");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Subject).HasMaxLength(120).IsRequired();
        builder.HasIndex(entity => entity.TeacherProfileId);
    }
}

internal sealed class TeacherCertificateConfiguration : IEntityTypeConfiguration<TeacherCertificate>
{
    public void Configure(EntityTypeBuilder<TeacherCertificate> builder)
    {
        builder.ToTable("teacher_certificates");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Institution).HasMaxLength(200);
        builder.Property(entity => entity.FileUrl).HasMaxLength(512);
        builder.HasIndex(entity => entity.TeacherProfileId);
    }
}

internal sealed class TeacherAvailabilitySlotConfiguration : IEntityTypeConfiguration<TeacherAvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<TeacherAvailabilitySlot> builder)
    {
        builder.ToTable("teacher_availability_slots");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.DayOfWeek).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.StartTime).IsRequired();
        builder.Property(entity => entity.EndTime).IsRequired();
        builder.HasIndex(entity => new { entity.TeacherProfileId, entity.DayOfWeek, entity.StartTime });
    }
}
