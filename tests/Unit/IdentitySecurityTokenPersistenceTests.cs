using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Tests.Unit.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P02 Task 3 regresyonu: izlenen bir <see cref="UserAccount"/> üzerine yeni bir güvenlik tokenı
/// eklendiğinde EF, istemcide üretilen Guid Id'yi <c>OnAdd</c> sanıp satırı UPDATE etmeye çalışıyordu
/// (<c>DbUpdateConcurrencyException</c>). Şifre sıfırlama ve e-posta doğrulama uçları bu yüzden 500 dönüyordu.
/// </summary>
public sealed class IdentitySecurityTokenPersistenceTests
{
    private static IdentityDbContext NewContext(string name)
        => new(
            new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(name).Options,
            new NoOpDomainEventMapper());

    private static UserAccount NewUser(Guid id) => new(
        id,
        "diag@example.com",
        "DIAG@EXAMPLE.COM",
        "hash",
        "Diag",
        "Kullanici",
        null,
        UserAccountStatus.PendingActivation,
        isEmailConfirmed: false,
        isProfileVerified: false,
        createdOnUtc: new DateTime(2026, 1, 1));

    [Fact]
    public async Task Adding_SecurityToken_To_Tracked_User_Should_Persist()
    {
        var dbName = $"identity-token-{Guid.NewGuid()}";
        var userId = Guid.NewGuid();

        await using (var context = NewContext(dbName))
        {
            context.Add(NewUser(userId));
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext(dbName))
        {
            var loaded = await context.UserAccounts.Include(u => u.SecurityTokens).SingleAsync();

            loaded.SecurityTokens.Add(new UserSecurityToken(
                Guid.NewGuid(),
                loaded.Id,
                SecurityTokenPurpose.PasswordReset,
                "token-hash",
                new DateTime(2026, 2, 1),
                new DateTime(2026, 2, 1, 1, 0, 0)));

            await context.SaveChangesAsync(); // burada DbUpdateConcurrencyException atıyordu
        }

        await using (var verify = NewContext(dbName))
        {
            var token = Assert.Single(await verify.UserSecurityTokens.ToListAsync());
            Assert.Equal(SecurityTokenPurpose.PasswordReset, token.Purpose);
        }
    }

    [Fact]
    public async Task Adding_RefreshSession_To_Tracked_User_Should_Persist()
    {
        var dbName = $"identity-session-{Guid.NewGuid()}";
        var userId = Guid.NewGuid();

        await using (var context = NewContext(dbName))
        {
            context.Add(NewUser(userId));
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext(dbName))
        {
            var loaded = await context.UserAccounts.Include(u => u.RefreshSessions).SingleAsync();

            loaded.RefreshSessions.Add(new RefreshTokenSession(
                Guid.NewGuid(),
                loaded.Id,
                "refresh-hash",
                "iPhone",
                new DateTime(2026, 2, 1),
                new DateTime(2026, 3, 1)));

            await context.SaveChangesAsync();
        }

        await using (var verify = NewContext(dbName))
        {
            Assert.Single(await verify.RefreshTokenSessions.ToListAsync());
        }
    }

    [Fact]
    public async Task Adding_RoleMembership_To_Tracked_User_Should_Persist()
    {
        var dbName = $"identity-role-{Guid.NewGuid()}";
        var userId = Guid.NewGuid();

        await using (var context = NewContext(dbName))
        {
            context.Add(NewUser(userId));
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext(dbName))
        {
            var loaded = await context.UserAccounts.Include(u => u.RoleMemberships).SingleAsync();

            loaded.RoleMemberships.Add(new UserRoleMembership(
                Guid.NewGuid(),
                loaded.Id,
                UserRole.Teacher,
                new DateTime(2026, 2, 1)));

            await context.SaveChangesAsync();
        }

        await using (var verify = NewContext(dbName))
        {
            Assert.Single(await verify.UserRoleMemberships.ToListAsync());
        }
    }
}
