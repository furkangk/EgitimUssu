using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.LessonSessions.Infrastructure;

public sealed class LessonSessionsDbContext : ModuleDbContext
{
    public const string SchemaName = "lesson_sessions";

    public LessonSessionsDbContext(
        DbContextOptions<LessonSessionsDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<LessonSession> LessonSessions => Set<LessonSession>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "LessonSessions";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LessonSessionsDbContext).Assembly);
    }
}

internal sealed class LessonSessionConfiguration : IEntityTypeConfiguration<LessonSession>
{
    public void Configure(EntityTypeBuilder<LessonSession> builder)
    {
        builder.ToTable("lesson_sessions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Subject).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.AttendanceStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.TopicTitle).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.CoveredContent).HasMaxLength(2000);
        builder.Property(entity => entity.TeacherNotes).HasMaxLength(2000);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.HasIndex(entity => entity.LessonScheduleId);
        builder.HasIndex(entity => new { entity.StudentId, entity.PlannedStartAtUtc });
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.PlannedStartAtUtc });
    }
}
