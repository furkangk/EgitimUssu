using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

public sealed class ReportingDbContext : ModuleDbContext
{
    public const string SchemaName = "reporting";

    public ReportingDbContext(
        DbContextOptions<ReportingDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<TeacherDashboardProjection> TeacherDashboardProjections => Set<TeacherDashboardProjection>();

    public DbSet<TeacherMonthlyProjection> TeacherMonthlyProjections => Set<TeacherMonthlyProjection>();

    public DbSet<StudentStudyProjection> StudentStudyProjections => Set<StudentStudyProjection>();

    public DbSet<StudentPerformanceProjection> StudentPerformanceProjections => Set<StudentPerformanceProjection>();

    public DbSet<StudentReport> StudentReports => Set<StudentReport>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Reporting";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TeacherDashboardProjection>(builder =>
        {
            builder.ToTable("teacher_dashboard_projections");
            builder.HasKey(item => item.TeacherUserId);
            // Id istemcide üretiliyor (Öğrenilenler #3) — EF'in OnAdd varsayımı yeni satırı Modified sanar.
            builder.Property(item => item.TeacherUserId).ValueGeneratedNever();
            builder.Property(item => item.LastAppliedEventId);
            builder.Property(item => item.UpdatedOnUtc);

            builder.HasMany(item => item.Items)
                .WithOne()
                .HasForeignKey(item => item.TeacherUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(item => item.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<TeacherDashboardItem>(builder =>
        {
            builder.ToTable("teacher_dashboard_items");
            builder.HasKey(item => new { item.TeacherUserId, item.Kind, item.ItemId });
            builder.Property(item => item.Kind).HasConversion<int>();
            builder.Property(item => item.Amount).HasPrecision(18, 2);
            builder.Property(item => item.Currency).HasMaxLength(8);
            builder.HasIndex(item => new { item.TeacherUserId, item.Kind, item.OccursAtUtc });
        });

        // M14-1: öğretmen aylık özeti (dönem kovası = (öğretmen, yıl, ay))
        modelBuilder.Entity<TeacherMonthlyProjection>(builder =>
        {
            builder.ToTable("teacher_monthly_projections");
            builder.HasKey(item => new { item.TeacherUserId, item.Year, item.Month });
            builder.Property(item => item.CollectedAmount).HasPrecision(18, 2);

            builder.HasMany(item => item.Items)
                .WithOne()
                .HasForeignKey(item => new { item.TeacherUserId, item.Year, item.Month })
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(item => item.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<TeacherMonthlyItem>(builder =>
        {
            builder.ToTable("teacher_monthly_items");
            builder.HasKey(item => new { item.TeacherUserId, item.Year, item.Month, item.Kind, item.ItemId });
            builder.Property(item => item.Kind).HasConversion<int>();
            builder.Property(item => item.Amount).HasPrecision(18, 2);
        });

        // M14-2: öğrenci haftalık çalışma (dönem kovası = (öğrenci, ISO yıl, ISO hafta))
        modelBuilder.Entity<StudentStudyProjection>(builder =>
        {
            builder.ToTable("student_study_projections");
            builder.HasKey(item => new { item.StudentId, item.Year, item.WeekOfYear });

            builder.HasMany(item => item.Items)
                .WithOne()
                .HasForeignKey(item => new { item.StudentId, item.Year, item.WeekOfYear })
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(item => item.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<StudentStudyItem>(builder =>
        {
            builder.ToTable("student_study_items");
            builder.HasKey(item => new { item.StudentId, item.Year, item.WeekOfYear, item.Kind, item.ItemId });
            builder.Property(item => item.Kind).HasConversion<int>();
        });

        // M14-3: öğrenci aylık performansı (dönem kovası = (öğrenci, ders, yıl, ay))
        modelBuilder.Entity<StudentPerformanceProjection>(builder =>
        {
            builder.ToTable("student_performance_projections");
            builder.HasKey(item => new { item.StudentId, item.Subject, item.Year, item.Month });
            builder.Property(item => item.Subject).HasMaxLength(120);

            builder.HasMany(item => item.Items)
                .WithOne()
                .HasForeignKey(item => new { item.StudentId, item.Subject, item.Year, item.Month })
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(item => item.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<StudentPerformanceItem>(builder =>
        {
            builder.ToTable("student_performance_items");
            builder.HasKey(item => new { item.StudentId, item.Subject, item.Year, item.Month, item.Kind, item.ItemKey });
            builder.Property(item => item.Subject).HasMaxLength(120);
            builder.Property(item => item.ItemKey).HasMaxLength(160);
            builder.Property(item => item.Kind).HasConversion<int>();
            builder.Property(item => item.Score).HasPrecision(8, 4);
        });

        // M14-4: üretilmiş PDF raporunun kaydı (projeksiyon değil, gerçek bir aggregate).
        modelBuilder.Entity<StudentReport>(builder =>
        {
            builder.ToTable("student_reports");
            builder.HasKey(report => report.Id);
            // Id istemcide üretiliyor (Öğrenilenler #3) — EF'in OnAdd varsayımı yeni satırı Modified sanar.
            builder.Property(report => report.Id).ValueGeneratedNever();
            builder.Property(report => report.StorageKey).HasMaxLength(512).IsRequired();
            builder.Property(report => report.FileName).HasMaxLength(256).IsRequired();
            builder.Property(report => report.ContentType).HasMaxLength(128).IsRequired();
            builder.HasIndex(report => new { report.StudentId, report.CreatedOnUtc });
        });
    }
}
