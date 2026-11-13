using EgitimUssu.Modules.Membership.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Membership.Infrastructure;

public sealed class MembershipDbContext : ModuleDbContext
{
    public const string SchemaName = "membership";

    public MembershipDbContext(
        DbContextOptions<MembershipDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();

    public DbSet<ReferralCode> ReferralCodes => Set<ReferralCode>();

    public DbSet<ReferralRedemption> ReferralRedemptions => Set<ReferralRedemption>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Membership";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MembershipDbContext).Assembly);
    }
}

internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plans");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Role).HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Tier).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.Period).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.Price).HasPrecision(18, 2);
        builder.Property(entity => entity.Currency).HasMaxLength(8).IsRequired();

        // Plan kodu istemciye ve ödeme sağlayıcısına giden kararlı anahtardır: çift olamaz.
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => new { entity.Role, entity.IsActive });
    }
}

internal sealed class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("user_subscriptions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.ProviderSubscriptionId).HasMaxLength(128);

        // Kullanıcı başına tek abonelik kaydı: "deneme bir kez" kuralı (HasUsedTrial) ancak bu
        // teklik altında anlamlıdır — ikinci bir satır kullanıcıya ikinci bir deneme hakkı verirdi.
        builder.HasIndex(entity => entity.UserId).IsUnique();
        builder.HasIndex(entity => new { entity.Status, entity.CurrentPeriodEndUtc });
        builder.HasIndex(entity => entity.ProviderSubscriptionId);
    }
}

internal sealed class ReferralCodeConfiguration : IEntityTypeConfiguration<ReferralCode>
{
    public void Configure(EntityTypeBuilder<ReferralCode> builder)
    {
        builder.ToTable("referral_codes");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(16).IsRequired();

        // Kod elle girilen bir anahtardır: çift olamaz. Sahip başına da tek kod tutulur.
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => entity.OwnerUserId).IsUnique();
    }
}

internal sealed class ReferralRedemptionConfiguration : IEntityTypeConfiguration<ReferralRedemption>
{
    public void Configure(EntityTypeBuilder<ReferralRedemption> builder)
    {
        builder.ToTable("referral_redemptions");
        builder.HasKey(entity => entity.Id);

        // "Bir kullanıcı ömrü boyunca bir referans ödülü alır" kuralını veritabanı da zorlar:
        // uygulama katmanındaki kontrol eşzamanlı iki istekte yarışabilir.
        builder.HasIndex(entity => entity.RedeemerUserId).IsUnique();
        builder.HasIndex(entity => entity.ReferrerUserId);
    }
}
