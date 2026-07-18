using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Study.Infrastructure;

public sealed class StudyDbContext : ModuleDbContext
{
    public const string SchemaName = "study";

    public StudyDbContext(
        DbContextOptions<StudyDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<StudySession> StudySessions => Set<StudySession>();

    public DbSet<TestResult> TestResults => Set<TestResult>();

    public DbSet<StudyGoal> StudyGoals => Set<StudyGoal>();

    public DbSet<StudyStreak> StudyStreaks => Set<StudyStreak>();

    public DbSet<Achievement> Achievements => Set<Achievement>();

    public DbSet<StudentAchievement> StudentAchievements => Set<StudentAchievement>();

    public DbSet<StudyTopic> StudyTopics => Set<StudyTopic>();

    public DbSet<StudyStudent> StudyStudents => Set<StudyStudent>();

    public DbSet<StudentSubjectCatalog> StudentSubjectCatalogs => Set<StudentSubjectCatalog>();

    public DbSet<StudentTopicCatalog> StudentTopicCatalogs => Set<StudentTopicCatalog>();

    public DbSet<StudyNote> StudyNotes => Set<StudyNote>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Study";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudyDbContext).Assembly);
    }
}

internal sealed class StudySessionConfiguration : IEntityTypeConfiguration<StudySession>
{
    public void Configure(EntityTypeBuilder<StudySession> builder)
    {
        builder.ToTable("study_sessions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Subject).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Topic).HasMaxLength(160);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.PersonalNote).HasMaxLength(1000);
        builder.Property(e => e.StartedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.StudentId, e.Status });
        builder.HasIndex(e => new { e.StudentId, e.StartedAtUtc });
    }
}

internal sealed class TestResultConfiguration : IEntityTypeConfiguration<TestResult>
{
    public void Configure(EntityTypeBuilder<TestResult> builder)
    {
        builder.ToTable("test_results");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Subject).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Topic).HasMaxLength(160);
        builder.Property(e => e.TestName).HasMaxLength(200);
        builder.Property(e => e.TestType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.Net).HasPrecision(7, 2);
        builder.HasIndex(e => new { e.StudentId, e.Subject, e.TakenOnUtc });
    }
}

internal sealed class StudyGoalConfiguration : IEntityTypeConfiguration<StudyGoal>
{
    public void Configure(EntityTypeBuilder<StudyGoal> builder)
    {
        builder.ToTable("study_goals");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Subject).HasMaxLength(120);
        builder.Property(e => e.StreakThresholdPercent).HasDefaultValue(60);
        builder.Property(e => e.TargetNet).HasPrecision(7, 2);
        builder.Property(e => e.TargetScore).HasPrecision(9, 2);
        builder.HasIndex(e => new { e.StudentId, e.IsActive });
    }
}

internal sealed class StudyStreakConfiguration : IEntityTypeConfiguration<StudyStreak>
{
    public void Configure(EntityTypeBuilder<StudyStreak> builder)
    {
        builder.ToTable("study_streaks");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.StudentId).IsUnique();
    }
}

internal sealed class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> builder)
    {
        builder.ToTable("achievements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.IconKey).HasMaxLength(64);
        builder.HasIndex(e => e.Code).IsUnique();

        builder.HasData(
            Seed("11111111-1111-1111-1111-111111111101", "FIRST_SESSION", "İlk Adım", "İlk çalışma seansını tamamladın.", AchievementCategory.Consistency, 1, "flag"),
            Seed("11111111-1111-1111-1111-111111111102", "SESSIONS_10", "Düzenli Çalışan", "10 çalışma seansı tamamladın.", AchievementCategory.Consistency, 10, "event_repeat"),
            Seed("11111111-1111-1111-1111-111111111103", "STREAK_3", "3 Günlük Seri", "3 gün üst üste çalıştın.", AchievementCategory.Streak, 3, "local_fire_department"),
            Seed("11111111-1111-1111-1111-111111111104", "STREAK_7", "7 Günlük Seri", "7 gün üst üste çalıştın.", AchievementCategory.Streak, 7, "local_fire_department"),
            Seed("11111111-1111-1111-1111-111111111105", "STREAK_30", "30 Günlük Seri", "30 gün üst üste çalıştın.", AchievementCategory.Streak, 30, "whatshot"),
            Seed("11111111-1111-1111-1111-111111111106", "HOURS_10", "10 Saat", "Toplam 10 saat çalıştın.", AchievementCategory.StudyTime, 600, "schedule"),
            Seed("11111111-1111-1111-1111-111111111107", "HOURS_50", "50 Saat", "Toplam 50 saat çalıştın.", AchievementCategory.StudyTime, 3000, "schedule"),
            Seed("11111111-1111-1111-1111-111111111108", "HOURS_100", "100 Saat", "Toplam 100 saat çalıştın.", AchievementCategory.StudyTime, 6000, "military_tech"),
            Seed("11111111-1111-1111-1111-111111111109", "FIRST_TEST", "İlk Deneme", "İlk denemeni girdin.", AchievementCategory.TestPerformance, 1, "quiz"),
            Seed("11111111-1111-1111-1111-111111111110", "TESTS_10", "Deneme Avcısı", "10 deneme girdin.", AchievementCategory.TestPerformance, 10, "fact_check"));
    }

    private static Achievement Seed(string id, string code, string title, string description, AchievementCategory category, int threshold, string iconKey)
        => new(Guid.Parse(id), code, title, description, category, threshold, iconKey);
}

internal sealed class StudentAchievementConfiguration : IEntityTypeConfiguration<StudentAchievement>
{
    public void Configure(EntityTypeBuilder<StudentAchievement> builder)
    {
        builder.ToTable("student_achievements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.AchievementCode).HasMaxLength(64).IsRequired();
        builder.HasIndex(e => new { e.StudentId, e.AchievementCode }).IsUnique();
    }
}

internal sealed class StudyTopicConfiguration : IEntityTypeConfiguration<StudyTopic>
{
    public void Configure(EntityTypeBuilder<StudyTopic> builder)
    {
        builder.ToTable("study_topics");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Subject).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Topic).HasMaxLength(160).IsRequired();
        builder.HasIndex(e => new { e.StudentId, e.Subject, e.Topic }).IsUnique();
    }
}

internal sealed class StudentSubjectCatalogConfiguration : IEntityTypeConfiguration<StudentSubjectCatalog>
{
    public void Configure(EntityTypeBuilder<StudentSubjectCatalog> builder)
    {
        builder.ToTable("student_subject_catalogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.ColorHex).HasMaxLength(9);
        builder.HasIndex(e => new { e.StudentId, e.Name });
    }
}

internal sealed class StudentTopicCatalogConfiguration : IEntityTypeConfiguration<StudentTopicCatalog>
{
    public void Configure(EntityTypeBuilder<StudentTopicCatalog> builder)
    {
        builder.ToTable("student_topic_catalogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(160).IsRequired();
        builder.HasIndex(e => new { e.SubjectId, e.OrderIndex });
        builder.HasIndex(e => e.StudentId);
    }
}

internal sealed class StudyNoteConfiguration : IEntityTypeConfiguration<StudyNote>
{
    public void Configure(EntityTypeBuilder<StudyNote> builder)
    {
        builder.ToTable("study_notes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Body).HasMaxLength(8000).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(120);
        builder.Property(e => e.Topic).HasMaxLength(160);
        builder.Property(e => e.AttachmentUrl).HasMaxLength(500);
        builder.HasIndex(e => new { e.StudentId, e.UpdatedOnUtc });
    }
}

internal sealed class StudyStudentConfiguration : IEntityTypeConfiguration<StudyStudent>
{
    public void Configure(EntityTypeBuilder<StudyStudent> builder)
    {
        builder.ToTable("study_students");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasIndex(e => e.UserId);
    }
}
