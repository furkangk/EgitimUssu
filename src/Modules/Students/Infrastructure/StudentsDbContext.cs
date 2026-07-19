using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Students.Infrastructure;

public sealed class StudentsDbContext : ModuleDbContext
{
    public const string SchemaName = "students";

    public StudentsDbContext(
        DbContextOptions<StudentsDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();

    public DbSet<StudentSubject> StudentSubjects => Set<StudentSubject>();

    public DbSet<TeacherStudentLink> TeacherStudentLinks => Set<TeacherStudentLink>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Students";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudentsDbContext).Assembly);
    }
}

internal sealed class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.ToTable("student_profiles");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.FullName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.GradeLevel).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.ContactEmail).HasMaxLength(320);
        builder.Property(entity => entity.ContactPhone).HasMaxLength(32);
        builder.Property(entity => entity.GoalSummary).HasMaxLength(1000);
        builder.Property(entity => entity.LevelNotes).HasMaxLength(1000);
        builder.Property(entity => entity.Origin).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.TargetExam).HasConversion<string>().HasMaxLength(16).IsRequired().HasDefaultValue(TargetExam.None);
        builder.Property(entity => entity.MembershipTier).HasConversion<string>().HasMaxLength(16).IsRequired().HasDefaultValue(MembershipTier.Free);
        builder.Property(entity => entity.DateOfBirth).HasColumnType("date");
        builder.Property(entity => entity.IsMerged).IsRequired().HasDefaultValue(false);
        builder.Property(entity => entity.MergedIntoStudentId);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.UpdatedOnUtc).IsRequired();
        builder.HasIndex(entity => entity.UserId).IsUnique();
        builder.HasIndex(entity => entity.CreatedByTeacherUserId);
        builder.HasMany(entity => entity.Subjects).WithOne().HasForeignKey(entity => entity.StudentProfileId);
    }
}

internal sealed class TeacherStudentLinkConfiguration : IEntityTypeConfiguration<TeacherStudentLink>
{
    public void Configure(EntityTypeBuilder<TeacherStudentLink> builder)
    {
        builder.ToTable("teacher_student_links");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Currency).HasMaxLength(8).IsRequired();
        builder.Property(entity => entity.AgreedRateAmount).HasPrecision(12, 2);
        builder.Property(entity => entity.InviteCode).HasMaxLength(8);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.UpdatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.StudentId }).IsUnique();
        builder.HasIndex(entity => entity.InviteCode);
    }
}

internal sealed class StudentSubjectConfiguration : IEntityTypeConfiguration<StudentSubject>
{
    public void Configure(EntityTypeBuilder<StudentSubject> builder)
    {
        builder.ToTable("student_subjects");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Subject).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.TargetLevel).HasMaxLength(120);
        builder.HasIndex(entity => new { entity.StudentProfileId, entity.Subject });
    }
}
