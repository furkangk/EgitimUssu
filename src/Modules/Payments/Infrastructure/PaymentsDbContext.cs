using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Payments.Infrastructure;

public sealed class PaymentsDbContext : ModuleDbContext
{
    public const string SchemaName = "payments";

    public PaymentsDbContext(
        DbContextOptions<PaymentsDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    public DbSet<ParentPaymentDeclaration> ParentPaymentDeclarations => Set<ParentPaymentDeclaration>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Payments";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
    }
}

internal sealed class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable("payment_records");
        builder.HasMany(x => x.Transactions).WithOne().HasForeignKey(x => x.PaymentRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Transactions).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ItemType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Currency).HasMaxLength(8).IsRequired();
        builder.Property(entity => entity.ExpectedAmount).HasPrecision(18, 2);
        builder.Property(entity => entity.CollectedAmount).HasPrecision(18, 2);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Notes).HasMaxLength(1000);
        builder.HasIndex(entity => new { entity.StudentId, entity.Status, entity.DueDateUtc });
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.DueDateUtc });
        builder.HasIndex(entity => new { entity.LessonScheduleId, entity.OccurrenceStartUtc }).IsUnique()
            .HasFilter("\"LessonScheduleId\" IS NOT NULL AND \"ItemType\" = 'LateCancellation'");
    }
}

internal sealed class ParentPaymentDeclarationConfiguration : IEntityTypeConfiguration<ParentPaymentDeclaration>
{
    public void Configure(EntityTypeBuilder<ParentPaymentDeclaration> builder)
    {
        builder.ToTable("parent_payment_declarations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.DeclaredAmount).HasPrecision(18, 2);
        builder.Property(entity => entity.Note).HasMaxLength(500);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.Status });
        builder.HasIndex(entity => entity.PaymentRecordId).IsUnique()
            .HasDatabaseName("ux_payment_declaration_pending")
            .HasFilter("\"Status\" = 'Declared'");
    }
}

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasIndex(x => new { x.PaymentRecordId, x.OccurredOnUtc });
    }
}
