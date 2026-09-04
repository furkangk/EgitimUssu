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
    }
}
