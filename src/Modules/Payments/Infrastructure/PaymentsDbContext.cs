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
        builder.HasIndex(entity => entity.PaymentRecordId);
    }
}
