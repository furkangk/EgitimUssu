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
        builder.Property(entity => entity.RefreshTokenHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.DeviceName).HasMaxLength(128);
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.ExpiresOnUtc).IsRequired();
        builder.HasIndex(entity => new { entity.UserAccountId, entity.ExpiresOnUtc });
    }
}

internal sealed class UserSecurityTokenConfiguration : IEntityTypeConfiguration<UserSecurityToken>
{
    public void Configure(EntityTypeBuilder<UserSecurityToken> builder)
    {
        builder.ToTable("user_security_tokens");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Purpose).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.TokenHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.CreatedOnUtc).IsRequired();
        builder.Property(entity => entity.ExpiresOnUtc).IsRequired();
        builder.Property(entity => entity.UsedOnUtc);
        builder.HasIndex(entity => new { entity.UserAccountId, entity.Purpose, entity.ExpiresOnUtc });
    }
}
