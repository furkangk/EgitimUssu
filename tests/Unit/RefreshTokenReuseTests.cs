using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// A-15: Rotasyon sonrası <b>iptal edilmiş</b> bir refresh token'ın tekrar sunulması ele geçirme
/// sinyalidir.
/// <para>
/// Meşru istemci her yenilemede yeni token alır ve eskisini bir daha göndermez; eski token'ın geri
/// gelmesi ya çalınmış kopyanın kullanıldığını ya da meşru istemcinin çalınmış olanın ardından
/// geldiğini gösterir. Hangisi olduğu ayırt edilemez, bu yüzden tüm oturumlar kapatılır ve
/// dolaşımdaki erişim token'ları iptal edilir. Eskiden yalnız 401 dönülüyordu — olay normal bir
/// oturum sonu gibi görünüyor, kimse haberdar olmuyordu.
/// </para>
/// </summary>
public sealed class RefreshTokenReuseTests
{
    private static readonly DateTime Now = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Reusing_A_Revoked_Refresh_Token_Should_Revoke_Every_Session()
    {
        var user = CreateUser(out var rotatedSession, out var liveSession);
        rotatedSession.Revoke(Now.AddMinutes(-5)); // meşru rotasyon
        var revocation = new SpyTokenRevocationService();
        var handler = CreateHandler(user, revocation);

        var result = await handler.Handle(new RefreshTokenCommand("rotated-token", "iPhone"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("identity.invalid_refresh_token", result.Error.Code);
        Assert.NotNull(liveSession.RevokedOnUtc);
        Assert.DoesNotContain(user.RefreshSessions, session => session.IsActive(Now));
        Assert.Equal(user.Id, revocation.RevokedUserId);
    }

    [Fact]
    public async Task Normal_Rotation_Should_Not_Trigger_Revocation()
    {
        var user = CreateUser(out _, out var liveSession);
        var revocation = new SpyTokenRevocationService();
        var handler = CreateHandler(user, revocation);

        var result = await handler.Handle(new RefreshTokenCommand("live-token", "iPhone"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(liveSession.RevokedOnUtc); // rotasyon yalnız kullanılanı iptal eder
        Assert.Single(user.RefreshSessions, session => session.RefreshTokenHash == "yeni-token-1" && session.IsActive(Now));
        // Diğer cihazın oturumuna dokunulmaz — rotasyon toplu iptal değildir.
        Assert.Contains(user.RefreshSessions, session => session.RefreshTokenHash == "rotated-token" && session.IsActive(Now));
        Assert.Null(revocation.RevokedUserId);
    }

    /// <summary>Süresi dolmuş (ama iptal edilmemiş) token normal bir oturum sonudur, ihlal değil.</summary>
    [Fact]
    public async Task Expired_Token_Should_Not_Trigger_Revocation()
    {
        var user = CreateUser(out _, out var liveSession);
        var expired = new RefreshTokenSession(
            Guid.NewGuid(), user.Id, "expired-token", "Android", Now.AddDays(-40), Now.AddDays(-10));
        user.RefreshSessions.Add(expired);
        var revocation = new SpyTokenRevocationService();
        var handler = CreateHandler(user, revocation);

        var result = await handler.Handle(new RefreshTokenCommand("expired-token", "Android"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(revocation.RevokedUserId);
        Assert.Null(liveSession.RevokedOnUtc); // diğer oturumlara dokunulmaz
    }

    private static RefreshTokenCommandHandler CreateHandler(UserAccount user, ITokenRevocationService revocation)
        => new(
            new FakeRepository(user),
            new FakeTokenIssuer(),
            new PassthroughTokenProtector(),
            revocation,
            new SequentialIdGenerator(),
            new FixedClock(Now));

    private static UserAccount CreateUser(out RefreshTokenSession rotated, out RefreshTokenSession live)
    {
        var user = new UserAccount(
            Guid.NewGuid(), "kullanici@example.com", "KULLANICI@EXAMPLE.COM", "hash",
            "Ada", "Yilmaz", null, UserAccountStatus.Active, false, false, Now.AddDays(-30));

        rotated = new RefreshTokenSession(Guid.NewGuid(), user.Id, "rotated-token", "iPhone", Now.AddDays(-10), Now.AddDays(20));
        live = new RefreshTokenSession(Guid.NewGuid(), user.Id, "live-token", "iPhone", Now.AddMinutes(-5), Now.AddDays(30));
        user.RefreshSessions.Add(rotated);
        user.RefreshSessions.Add(live);
        return user;
    }

    private sealed class SpyTokenRevocationService : ITokenRevocationService
    {
        public Guid? RevokedUserId { get; private set; }

        public Task RevokeAllAccessTokensAsync(Guid userId, CancellationToken cancellationToken)
        {
            RevokedUserId = userId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepository(UserAccount user) : IUserAccountRepository
    {
        public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(user.Id == userId ? user : null);

        public Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(user.NormalizedEmail == normalizedEmail ? user : null);

        public Task<UserAccount?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(
                user.RefreshSessions.Any(session => session.RefreshTokenHash == refreshTokenHash) ? user : null);

        public Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PurgeStaleCredentialsAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Testte hash'leme kimlik dönüşümüdür; oturum arama davranışı bozulmadan okunur kalır.</summary>
    private sealed class PassthroughTokenProtector : ITokenProtector
    {
        private int _counter;

        public string Hash(string token) => token;

        public string GenerateToken() => $"yeni-token-{++_counter}";
    }

    private sealed class FakeTokenIssuer : ITokenIssuer
    {
        public (string AccessToken, DateTime ExpiresAtUtc) Issue(UserAccount userAccount)
            => ("access-token", Now.AddHours(1));
    }

    private sealed class SequentialIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}
