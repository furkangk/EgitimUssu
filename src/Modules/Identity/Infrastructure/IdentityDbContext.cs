using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Identity.Infrastructure;

public sealed class IdentityDbContext : ModuleDbContext
{
    public const string SchemaName = "identity";

    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<UserRoleMembership> UserRoleMemberships => Set<UserRoleMembership>();

    public DbSet<RefreshTokenSession> RefreshTokenSessions => Set<RefreshTokenSession>();
    public DbSet<UserSecurityToken> UserSecurityTokens => Set<UserSecurityToken>();

    /// <summary>E-07: Bekleyen/iptal edilen/tamamlanan hesap silme talepleri.</summary>
    public DbSet<AccountDeletionRequest> AccountDeletionRequests => Set<AccountDeletionRequest>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Identity";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_accounts");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Email).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.LastName).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.PhoneNumber).HasMaxLength(32);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.UpdatedOnUtc).IsRequired();
        builder.HasIndex(entity => entity.NormalizedEmail).IsUnique();
        builder.HasMany(entity => entity.RoleMemberships).WithOne().HasForeignKey(entity => entity.UserAccountId);
        builder.HasMany(entity => entity.RefreshSessions).WithOne().HasForeignKey(entity => entity.UserAccountId);
        builder.HasMany(entity => entity.SecurityTokens).WithOne().HasForeignKey(entity => entity.UserAccountId);
    }
}

internal sealed class UserRoleMembershipConfiguration : IEntityTypeConfiguration<UserRoleMembership>
{
    public void Configure(EntityTypeBuilder<UserRoleMembership> builder)
    {
        builder.ToTable("user_role_memberships");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.AssignedOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.UserAccountId, entity.Role }).IsUnique();
    }
}

internal sealed class RefreshTokenSessionConfiguration : IEntityTypeConfiguration<RefreshTokenSession>
{
    public void Configure(EntityTypeBuilder<RefreshTokenSession> builder)
    {
        builder.ToTable("refresh_token_sessions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.RefreshTokenHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.DeviceName).HasMaxLength(128);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.ExpiresOnUtc).IsRequired();
        // M01-4: Var olan satırlar için varsayılan CreatedOnUtc'dir (migration onu kopyalar).
        builder.Property(entity => entity.LastUsedOnUtc).IsRequired();
        // P15 T7 / N-29: İptal nedeni metin olarak saklanır (enum sırası değişse de veri bozulmaz).
        // Migration var olan iptal edilmiş satırları Rotated kabul eder (A-15 davranışı korunur).
        builder.Property(entity => entity.RevocationReason).HasConversion<string>().HasMaxLength(32);
        // Cihaz oturumu listesi: kullanıcının AKTİF oturumlarını okur.
        builder.HasIndex(entity => new { entity.UserAccountId, entity.RevokedOnUtc });
        builder.HasIndex(entity => new { entity.UserAccountId, entity.ExpiresOnUtc });
    }
}

internal sealed class UserSecurityTokenConfiguration : IEntityTypeConfiguration<UserSecurityToken>
{
    public void Configure(EntityTypeBuilder<UserSecurityToken> builder)
    {
        builder.ToTable("user_security_tokens");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.Purpose).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.TokenHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.ExpiresOnUtc).IsRequired();
        builder.Property(entity => entity.UsedOnUtc);
        builder.HasIndex(entity => new { entity.UserAccountId, entity.Purpose, entity.ExpiresOnUtc });
    }
}

/// <summary>E-07: Hesap silme talebi eşlemesi.</summary>
internal sealed class AccountDeletionRequestConfiguration : IEntityTypeConfiguration<AccountDeletionRequest>
{
    /// <summary>P15 T7 / N-32: Kullanıcı başına tek bekleyen talep — kısmi benzersiz indeks.</summary>
    public const string PendingRequestIndexName = "IX_account_deletion_requests_UserAccountId_pending";

    public void Configure(EntityTypeBuilder<AccountDeletionRequest> builder)
    {
        builder.ToTable("account_deletion_requests");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.UserAccountId).IsRequired();
        builder.Property(entity => entity.RequestedOnUtc).IsRequired();
        builder.Property(entity => entity.ScheduledForUtc).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // Kullanıcının talep geçmişi (iptal/tamamlanmış talepler kayıt olarak kalır: "ne zaman istedi,
        // ne zaman vazgeçti" hesap verebilirliğin parçasıdır).
        builder.HasIndex(entity => new { entity.UserAccountId, entity.Status });

        // P15 T7 / N-32: Bir kullanıcının aynı anda birden çok BEKLEYEN talebi olamaz. Eskiden bu yalnız
        // yorumda yazıyordu (indeks benzersiz değildi) — eşzamanlı çift dokunuş iki talep açıyordu.
        // Adlı aşırı yükleme şart: aynı kolonda ikinci indeks (#195a).
        builder.HasIndex(entity => entity.UserAccountId, PendingRequestIndexName)
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");

        // Bakım servisi "süresi dolan bekleyenler" sorgusunu bu indeksle koşar.
        builder.HasIndex(entity => new { entity.Status, entity.ScheduledForUtc });
    }
}
