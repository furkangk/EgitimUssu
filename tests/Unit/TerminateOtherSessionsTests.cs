using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// M15-3: `SessionTerminationPolicy.TerminateOtherSessions` seçiliyken yeni giriş,
/// kullanıcının önceki refresh oturumlarını iptal eder ("tek cihazda açık kal").
/// </summary>
public sealed class TerminateOtherSessionsTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Login_Should_Revoke_Previous_Sessions_When_Policy_Enabled()
    {
        var user = CreateUser(out var oldSession);
        var handler = CreateHandler(user, terminateOtherSessions: true);

        var result = await handler.Handle(new LoginUserCommand("kullanici@example.com", "Sifre123!", "iPhone"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(oldSession.RevokedOnUtc);
        Assert.Equal(Now, oldSession.RevokedOnUtc);
        // Yeni oturum ayakta kalmalı.
        var active = user.RefreshSessions.Where(s => s.IsActive(Now)).ToList();
        Assert.Single(active);
        Assert.Equal("iPhone", active[0].DeviceName);
    }

    [Fact]
    public async Task Login_Should_Keep_Previous_Sessions_When_Policy_Disabled()
    {
        var user = CreateUser(out var oldSession);
        var handler = CreateHandler(user, terminateOtherSessions: false);

        var result = await handler.Handle(new LoginUserCommand("kullanici@example.com", "Sifre123!", "iPhone"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(oldSession.RevokedOnUtc);
        Assert.Equal(2, user.RefreshSessions.Count(s => s.IsActive(Now)));
    }

    [Fact]
    public void RevokeOtherSessions_Should_Not_Touch_Kept_Session_Or_Already_Revoked()
    {
        var user = CreateUser(out var oldSession);
        var kept = new RefreshTokenSession(Guid.NewGuid(), user.Id, "yeni-hash", "iPhone", Now, Now.AddDays(30));
        user.RefreshSessions.Add(kept);
        var alreadyRevoked = new RefreshTokenSession(Guid.NewGuid(), user.Id, "eski2-hash", "Web", Now.AddDays(-5), Now.AddDays(25));
        alreadyRevoked.Revoke(Now.AddDays(-2));
        user.RefreshSessions.Add(alreadyRevoked);

        user.RevokeOtherSessions(kept.Id, Now);

        Assert.Null(kept.RevokedOnUtc);
        Assert.Equal(Now, oldSession.RevokedOnUtc);
        // Zaten iptal edilen kaydın zaman damgası korunur.
        Assert.Equal(Now.AddDays(-2), alreadyRevoked.RevokedOnUtc);
    }

    private static LoginUserCommandHandler CreateHandler(UserAccount user, bool terminateOtherSessions)
        => new(
            new FakeUserAccountRepository(user),
            new FakePasswordHasher(),
            new FakeTokenIssuer(),
            new FakeTokenProtector(),
            new NoopThrottle(),
            new FakeSessionPolicyDirectory(terminateOtherSessions),
            new FakeIdGenerator(),
            new FixedClock(Now));

    private static UserAccount CreateUser(out RefreshTokenSession existingSession)
    {
        var user = new UserAccount(
            Guid.NewGuid(),
            "kullanici@example.com",
            "KULLANICI@EXAMPLE.COM",
            "hash",
            "Ad",
            "Soyad",
            "5551112233",
            UserAccountStatus.Active,
            isEmailConfirmed: true,
            isProfileVerified: false,
            Now.AddDays(-30));

        existingSession = new RefreshTokenSession(Guid.NewGuid(), user.Id, "eski-hash", "Android", Now.AddDays(-10), Now.AddDays(20));
        user.RefreshSessions.Add(existingSession);
        return user;
    }

    private sealed class FakeSessionPolicyDirectory(bool value) : ISessionPolicyDirectory
    {
        public Task<bool> ShouldTerminateOtherSessionsAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(value);
    }

    private sealed class FakeUserAccountRepository(UserAccount user) : IUserAccountRepository
    {
        public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(user.Id == userId ? user : null);

        public Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(user.NormalizedEmail == normalizedEmail ? user : null);

        public Task<UserAccount?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(user.RefreshSessions.Any(s => s.RefreshTokenHash == refreshTokenHash) ? user : null);

        public Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash";
        public bool Verify(string hashedPassword, string providedPassword) => true;
    }

    private sealed class FakeTokenIssuer : ITokenIssuer
    {
        public (string AccessToken, DateTime ExpiresAtUtc) Issue(UserAccount userAccount) => ("access", Now.AddMinutes(15));
    }

    private sealed class FakeTokenProtector : ITokenProtector
    {
        private int _counter;
        public string Hash(string token) => token + "-hash";
        public string GenerateToken() => $"refresh-{++_counter}";
    }

    private sealed class NoopThrottle : ILoginAttemptThrottle
    {
        public Task<bool> IsLockedAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task RecordFailureAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResetAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
