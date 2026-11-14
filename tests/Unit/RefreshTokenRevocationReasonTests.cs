using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P15 Task 7 / N-29, N-30, N-31, N-32: Oturum iptalinin <b>nedeni</b> saklanır ve yeniden kullanım
/// (A-15) yalnız gerçekten rotasyonla eskimiş bir token'da tetiklenir.
/// <para>
/// Eski davranış: iptal edilmiş <b>her</b> refresh token'ın tekrar sunulması ele geçirme sayılıyordu.
/// "Diğer oturumları kapat" ile kapatılan tablet bir sonraki açılışta refresh yapınca, kullanıcının
/// az önce açtığı telefon oturumu da ölüyordu (N-29). Eşzamanlı iki refresh'in kaybedeni de toplu
/// iptali tetikliyordu.
/// </para>
/// </summary>
public sealed class RefreshTokenRevocationReasonTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    // --- Domain --------------------------------------------------------------------

    [Fact]
    public void Revoke_Records_The_Reason_And_The_First_Revocation_Wins()
    {
        var session = NewSession("token");

        session.Revoke(Now, SessionRevocationReason.DeviceRevoked);
        session.Revoke(Now.AddMinutes(5), SessionRevocationReason.Rotated);

        Assert.Equal(Now, session.RevokedOnUtc);
        Assert.Equal(SessionRevocationReason.DeviceRevoked, session.RevocationReason);
    }

    [Fact]
    public void RevokeOtherSessions_Returns_Only_The_Sessions_It_Revoked()
    {
        var user = NewUser();
        var kept = AddSession(user, "kept");
        var other = AddSession(user, "other");
        var alreadyRevoked = AddSession(user, "already");
        alreadyRevoked.Revoke(Now.AddDays(-1), SessionRevocationReason.UserLogout);

        var revoked = user.RevokeOtherSessions(kept.Id, Now, SessionRevocationReason.OtherSessionsRevoked);

        Assert.Equal([other.Id], revoked);
        Assert.Null(kept.RevokedOnUtc);
        Assert.Equal(SessionRevocationReason.OtherSessionsRevoked, other.RevocationReason);
        // Zaten kapalı olanın nedeni ve zamanı korunur.
        Assert.Equal(SessionRevocationReason.UserLogout, alreadyRevoked.RevocationReason);
        Assert.Equal(Now.AddDays(-1), alreadyRevoked.RevokedOnUtc);
    }

    // --- Refresh: rotasyon ve yeniden kullanım ---------------------------------------

    [Fact]
    public async Task Rotation_Revokes_The_Presented_Session_As_Rotated()
    {
        var user = NewUser();
        var presented = AddSession(user, "live");
        var repository = new FakeRepository(user);
        var handler = CreateRefreshHandler(repository, new SpyRevocation());

        var result = await handler.Handle(new RefreshTokenCommand("live", "iPhone"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionRevocationReason.Rotated, presented.RevocationReason);
        Assert.Equal(presented.Id, repository.RotatedFrom?.Id);
    }

    /// <summary>
    /// N-30: Koşullu güncellemeyi başka bir istek kazandıysa kaybeden düz 401 alır; kimseyi iptal etmez.
    /// </summary>
    [Fact]
    public async Task Losing_The_Rotation_Race_Returns_401_Without_Revoking_Anything()
    {
        var user = NewUser();
        AddSession(user, "live");
        var otherDevice = AddSession(user, "tablet");
        var revocation = new SpyRevocation();
        var handler = CreateRefreshHandler(new FakeRepository(user) { RotationWins = false }, revocation);

        var result = await handler.Handle(new RefreshTokenCommand("live", "iPhone"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("identity.invalid_refresh_token", result.Error.Code);
        Assert.Null(otherDevice.RevokedOnUtc);
        Assert.Null(revocation.AllRevokedUserId);
    }

    /// <summary>
    /// Bekleme penceresi içinde (eşzamanlı refresh'in kaybedeni, zaman aşımından sonra yeniden deneme)
    /// rotasyonla eskimiş token düz 401'dir.
    /// </summary>
    [Fact]
    public async Task Rotated_Token_Within_Grace_Returns_401_Without_Revoking_Anything()
    {
        var user = NewUser();
        var rotated = AddSession(user, "rotated");
        rotated.Revoke(Now.AddSeconds(-29), SessionRevocationReason.Rotated);
        var otherDevice = AddSession(user, "tablet");
        var revocation = new SpyRevocation();
        var handler = CreateRefreshHandler(new FakeRepository(user), revocation);

        var result = await handler.Handle(new RefreshTokenCommand("rotated", "iPhone"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("identity.invalid_refresh_token", result.Error.Code);
        Assert.Null(otherDevice.RevokedOnUtc);
        Assert.Null(revocation.AllRevokedUserId);
    }

    /// <summary>A-15 korunur: rotasyondan 30 sn'den fazla sonra eski token gelirse her oturum kapanır.</summary>
    [Fact]
    public async Task Rotated_Token_After_Grace_Revokes_Every_Session()
    {
        var user = NewUser();
        var rotated = AddSession(user, "rotated");
        rotated.Revoke(Now.AddSeconds(-31), SessionRevocationReason.Rotated);
        var otherDevice = AddSession(user, "tablet");
        var revocation = new SpyRevocation();
        var handler = CreateRefreshHandler(new FakeRepository(user), revocation);

        var result = await handler.Handle(new RefreshTokenCommand("rotated", "iPhone"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SessionRevocationReason.ReuseDetected, otherDevice.RevocationReason);
        Assert.Equal(user.Id, revocation.AllRevokedUserId);
    }

    /// <summary>
    /// N-29: Rotasyon dışındaki bir nedenle kapanmış oturumun token'ı ne kadar sonra gelirse gelsin
    /// ele geçirme sinyali değildir — kullanıcı o oturumu bilerek kapatmıştır.
    /// </summary>
    [Theory]
    [InlineData(SessionRevocationReason.UserLogout)]
    [InlineData(SessionRevocationReason.DeviceRevoked)]
    [InlineData(SessionRevocationReason.OtherSessionsRevoked)]
    [InlineData(SessionRevocationReason.PasswordReset)]
    [InlineData(SessionRevocationReason.ReuseDetected)]
    public async Task Deliberately_Closed_Session_Never_Triggers_Mass_Revocation(SessionRevocationReason reason)
    {
        var user = NewUser();
        var closed = AddSession(user, "closed");
        closed.Revoke(Now.AddDays(-2), reason);
        var phone = AddSession(user, "phone");
        var revocation = new SpyRevocation();
        var handler = CreateRefreshHandler(new FakeRepository(user), revocation);

        var result = await handler.Handle(new RefreshTokenCommand("closed", "Tablet"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("identity.invalid_refresh_token", result.Error.Code);
        Assert.Null(phone.RevokedOnUtc);
        Assert.Null(revocation.AllRevokedUserId);
        Assert.Empty(revocation.RevokedSessionIds);
    }

    // --- sid iptali (N-31) -----------------------------------------------------------

    [Fact]
    public async Task Login_With_Terminate_Policy_Closes_Others_As_OtherSessionsRevoked_And_Blocks_Their_Sids()
    {
        var user = NewUser();
        var tablet = AddSession(user, "tablet");
        var revocation = new SpyRevocation();
        var handler = new LoginUserCommandHandler(
            new FakeRepository(user), new SpyPasswordHasher(verifies: true), new FakeTokenIssuer(),
            new PassthroughTokenProtector(), new NoThrottle(), new FixedSessionPolicy(terminate: true),
            revocation, new GuidIdGenerator(), new FixedClock(Now));

        var result = await handler.Handle(new LoginUserCommand(user.Email, "Sifre123!", "iPhone"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionRevocationReason.OtherSessionsRevoked, tablet.RevocationReason);
        Assert.Equal([tablet.Id], revocation.RevokedSessionIds);
    }

    [Fact]
    public async Task Revoking_A_Device_Session_Blocks_Its_Sid()
    {
        var user = NewUser();
        var current = AddSession(user, "phone");
        var tablet = AddSession(user, "tablet");
        var revocation = new SpyRevocation();
        var handler = new RevokeDeviceSessionCommandHandler(
            new FakeRepository(user), new FakeCurrentUser(user.Id, current.Id), revocation, new FixedClock(Now));

        var result = await handler.Handle(new RevokeDeviceSessionCommand(tablet.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionRevocationReason.DeviceRevoked, tablet.RevocationReason);
        Assert.Equal([tablet.Id], revocation.RevokedSessionIds);
        Assert.Null(current.RevokedOnUtc);
    }

    [Fact]
    public async Task Revoke_All_Blocks_Every_Other_Sid_But_Not_The_Current_One()
    {
        var user = NewUser();
        var current = AddSession(user, "phone");
        var tablet = AddSession(user, "tablet");
        var laptop = AddSession(user, "laptop");
        var revocation = new SpyRevocation();
        var handler = new RevokeAllDeviceSessionsCommandHandler(
            new FakeRepository(user), new FakeCurrentUser(user.Id, current.Id), revocation, new FixedClock(Now));

        var result = await handler.Handle(new RevokeAllDeviceSessionsCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
        Assert.Equal(new HashSet<Guid> { tablet.Id, laptop.Id }, revocation.RevokedSessionIds.ToHashSet());
        Assert.Equal(SessionRevocationReason.OtherSessionsRevoked, laptop.RevocationReason);
        Assert.Null(current.RevokedOnUtc);
    }

    [Fact]
    public async Task Logout_Records_UserLogout_And_Blocks_The_Sid()
    {
        var user = NewUser();
        var session = AddSession(user, "phone");
        var revocation = new SpyRevocation();
        var handler = new LogoutCommandHandler(
            new FakeRepository(user), new PassthroughTokenProtector(), revocation, new FixedClock(Now));

        var result = await handler.Handle(new LogoutCommand("phone"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionRevocationReason.UserLogout, session.RevocationReason);
        Assert.Equal([session.Id], revocation.RevokedSessionIds);
    }

    [Fact]
    public async Task Password_Reset_Records_PasswordReset_Reason()
    {
        var user = NewUser();
        var session = AddSession(user, "phone");
        user.SecurityTokens.Add(new UserSecurityToken(
            Guid.NewGuid(), user.Id, SecurityTokenPurpose.PasswordReset, "reset", Now.AddMinutes(-5), Now.AddMinutes(55)));
        var revocation = new SpyRevocation();
        var handler = new ResetPasswordCommandHandler(
            new FakeRepository(user), new PassthroughTokenProtector(), new SpyPasswordHasher(verifies: true),
            revocation, new FixedClock(Now));

        var result = await handler.Handle(new ResetPasswordCommand(user.Email, "reset", "YeniSifre123!"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionRevocationReason.PasswordReset, session.RevocationReason);
        Assert.Equal(user.Id, revocation.AllRevokedUserId);
    }

    // --- Giriş zamanlaması (N-32) ----------------------------------------------------

    /// <summary>
    /// Var olmayan e-postayla giriş de parola özetini doğrular; aksi halde yanıt süresi farkı hangi
    /// adreslerin kayıtlı olduğunu söyler (PBKDF2 ~100 ms, "kullanıcı yok" ~1 ms).
    /// </summary>
    [Fact]
    public async Task Login_With_Unknown_Email_Still_Runs_Password_Verification()
    {
        var hasher = new SpyPasswordHasher(verifies: false);
        var handler = new LoginUserCommandHandler(
            new FakeRepository(NewUser()), hasher, new FakeTokenIssuer(), new PassthroughTokenProtector(),
            new NoThrottle(), new FixedSessionPolicy(terminate: false), new SpyRevocation(),
            new GuidIdGenerator(), new FixedClock(Now));

        var result = await handler.Handle(new LoginUserCommand("yok@example.com", "Sifre123!", null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("identity.invalid_credentials", result.Error.Code);
        Assert.Equal(1, hasher.VerifyCalls);
    }

    // --- Yardımcılar -----------------------------------------------------------------

    private static RefreshTokenCommandHandler CreateRefreshHandler(FakeRepository repository, SpyRevocation revocation)
        => new(
            repository,
            new FakeTokenIssuer(),
            new PassthroughTokenProtector(),
            revocation,
            new RefreshTokenPolicy(Grace),
            new GuidIdGenerator(),
            new FixedClock(Now));

    private static UserAccount NewUser()
        => new(
            Guid.NewGuid(), "kullanici@example.com", "KULLANICI@EXAMPLE.COM", "hash",
            "Ada", "Yilmaz", null, UserAccountStatus.Active, false, false, Now.AddDays(-30));

    private static RefreshTokenSession NewSession(string tokenHash)
        => new(Guid.NewGuid(), Guid.NewGuid(), tokenHash, null, Now.AddDays(-1), Now.AddDays(29));

    private static RefreshTokenSession AddSession(UserAccount user, string tokenHash)
    {
        var session = new RefreshTokenSession(Guid.NewGuid(), user.Id, tokenHash, tokenHash, Now.AddDays(-1), Now.AddDays(29));
        user.RefreshSessions.Add(session);
        return session;
    }

    private sealed class SpyRevocation : ITokenRevocationService
    {
        public Guid? AllRevokedUserId { get; private set; }

        public List<Guid> RevokedSessionIds { get; } = [];

        public Task RevokeAllAccessTokensAsync(Guid userId, CancellationToken cancellationToken)
        {
            AllRevokedUserId = userId;
            return Task.CompletedTask;
        }

        public Task RevokeSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken)
        {
            RevokedSessionIds.AddRange(sessionIds);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepository(UserAccount user) : IUserAccountRepository
    {
        public bool RotationWins { get; init; } = true;

        public RefreshTokenSession? RotatedFrom { get; private set; }

        public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(user.Id == userId ? user : null);

        public Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(user.NormalizedEmail == normalizedEmail ? user : null);

        public Task<UserAccount?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(
                user.RefreshSessions.Any(session => session.RefreshTokenHash == refreshTokenHash) ? user : null);

        public Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> SaveRotationAsync(RefreshTokenSession rotatedFrom, CancellationToken cancellationToken)
        {
            RotatedFrom = rotatedFrom;
            return Task.FromResult(RotationWins);
        }

        public Task PurgeStaleCredentialsAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SpyPasswordHasher(bool verifies) : IPasswordHasher
    {
        public int VerifyCalls { get; private set; }

        public string Hash(string password) => $"hash:{password}";

        public bool Verify(string hashedPassword, string providedPassword)
        {
            VerifyCalls++;
            return verifies;
        }
    }

    private sealed class PassthroughTokenProtector : ITokenProtector
    {
        private int _counter;

        public string Hash(string token) => token;

        public string GenerateToken() => $"yeni-token-{++_counter}";
    }

    private sealed class FakeTokenIssuer : ITokenIssuer
    {
        public (string AccessToken, DateTime ExpiresAtUtc) Issue(UserAccount userAccount, Guid sessionId)
            => ("access-token", Now.AddHours(1));
    }

    private sealed class NoThrottle : ILoginAttemptThrottle
    {
        public Task<bool> IsLockedAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task RecordFailureAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResetAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedSessionPolicy(bool terminate) : ISessionPolicyDirectory
    {
        public Task<bool> ShouldTerminateOtherSessionsAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(terminate);
    }

    private sealed class FakeCurrentUser(Guid userId, Guid sessionId) : ICurrentUser
    {
        public string? UserId => userId.ToString();

        public string? Email => "kullanici@example.com";

        public IReadOnlyCollection<string> Roles => ["Teacher"];

        public bool IsAuthenticated => true;

        public Guid? SessionId => sessionId;
    }

    private sealed class GuidIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}
