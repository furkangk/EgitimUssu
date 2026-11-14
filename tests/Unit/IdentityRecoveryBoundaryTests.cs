using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class IdentityRecoveryBoundaryTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(UserAccountStatus.Suspended)]
    [InlineData(UserAccountStatus.Closed)]
    public async Task Refresh_Rejects_Inactive_Account_Without_Issuing_Or_Rotating_Tokens(UserAccountStatus status)
    {
        var user = CreateUser(status);
        var session = AddSession(user);
        var repository = new Repository(user);
        var issuer = new TokenIssuer();
        var handler = new RefreshTokenCommandHandler(repository, issuer, new TokenProtector(), new TokenRevocation(), new RefreshTokenPolicy(RefreshTokenPolicy.DefaultReuseGrace), new IdGenerator(), new Clock());

        var result = await handler.Handle(new RefreshTokenCommand("refresh", null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("identity.invalid_refresh_token", result.Error.Code);
        Assert.Equal(0, issuer.IssueCount);
        Assert.Equal(0, repository.SaveCount);
        Assert.Single(user.RefreshSessions);
        Assert.Null(session.RevokedOnUtc);
    }

    [Theory]
    [InlineData(UserAccountStatus.Active)]
    [InlineData(UserAccountStatus.PendingActivation)]
    public async Task Refresh_Still_Rotates_Tokens_For_Login_Eligible_Accounts(UserAccountStatus status)
    {
        var user = CreateUser(status);
        var session = AddSession(user);
        var repository = new Repository(user);
        var issuer = new TokenIssuer();
        var handler = new RefreshTokenCommandHandler(repository, issuer, new TokenProtector(), new TokenRevocation(), new RefreshTokenPolicy(RefreshTokenPolicy.DefaultReuseGrace), new IdGenerator(), new Clock());

        var result = await handler.Handle(new RefreshTokenCommand("refresh", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now, session.RevokedOnUtc);
        Assert.Single(user.RefreshSessions, item => item.IsActive(Now));
        Assert.Equal(1, issuer.IssueCount);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Reset_Consumes_All_Pending_Reset_Links_And_Rejects_Older_Link_Reuse()
    {
        var user = CreateUser(UserAccountStatus.Active);
        var session = AddSession(user);
        var older = AddSecurityToken(user, "older", SecurityTokenPurpose.PasswordReset);
        var current = AddSecurityToken(user, "current", SecurityTokenPurpose.PasswordReset);
        var verification = AddSecurityToken(user, "verification", SecurityTokenPurpose.EmailVerification);
        var repository = new Repository(user);
        var handler = new ResetPasswordCommandHandler(repository, new TokenProtector(), new PasswordHasher(), new TokenRevocation(), new Clock());

        var reset = await handler.Handle(new ResetPasswordCommand(user.Email, "current", "Recovered123!"), CancellationToken.None);
        var replay = await handler.Handle(new ResetPasswordCommand(user.Email, "older", "Compromised123!"), CancellationToken.None);

        Assert.True(reset.IsSuccess);
        Assert.True(replay.IsFailure);
        Assert.Equal("identity.invalid_password_reset_token", replay.Error.Code);
        Assert.False(older.IsUsable(Now));
        Assert.False(current.IsUsable(Now));
        Assert.True(verification.IsUsable(Now));
        Assert.Equal("hash:Recovered123!", user.PasswordHash);
        Assert.Equal(Now, session.RevokedOnUtc);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Invalid_Reset_Link_Does_Not_Invalidate_Valid_Link()
    {
        var user = CreateUser(UserAccountStatus.Active);
        var token = AddSecurityToken(user, "current", SecurityTokenPurpose.PasswordReset);
        var repository = new Repository(user);
        var handler = new ResetPasswordCommandHandler(repository, new TokenProtector(), new PasswordHasher(), new TokenRevocation(), new Clock());

        var result = await handler.Handle(new ResetPasswordCommand(user.Email, "invalid", "Recovered123!"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.True(token.IsUsable(Now));
        Assert.Equal("original-hash", user.PasswordHash);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Registration_With_Null_Roles_Is_Validation_Failure()
    {
        var result = await new RegisterUserCommandValidator().Validate(
            new RegisterUserCommand("user@example.com", "Password123!", "Ad", "Soyad", null, null!),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("identity.invalid_request", result.Error.Code);
    }

    private static UserAccount CreateUser(UserAccountStatus status)
        => new(Guid.NewGuid(), "user@example.com", "USER@EXAMPLE.COM", "original-hash", "Ad", "Soyad", null,
            status, true, false, Now.AddDays(-1));

    private static RefreshTokenSession AddSession(UserAccount user)
    {
        var session = new RefreshTokenSession(Guid.NewGuid(), user.Id, "hash:refresh", "Phone", Now.AddDays(-1), Now.AddDays(29));
        user.RefreshSessions.Add(session);
        return session;
    }

    private static UserSecurityToken AddSecurityToken(UserAccount user, string token, SecurityTokenPurpose purpose)
    {
        var securityToken = new UserSecurityToken(Guid.NewGuid(), user.Id, purpose, "hash:" + token, Now.AddMinutes(-10), Now.AddHours(1));
        user.SecurityTokens.Add(securityToken);
        return securityToken;
    }

    private sealed class Repository(UserAccount user) : IUserAccountRepository
    {
        public int SaveCount { get; private set; }
        public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(user.Id == userId ? user : null);
        public Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(user.NormalizedEmail == normalizedEmail ? user : null);
        public Task<UserAccount?> GetByRefreshTokenHashAsync(string hash, CancellationToken cancellationToken)
            => Task.FromResult<UserAccount?>(user.RefreshSessions.Any(session => session.RefreshTokenHash == hash) ? user : null);
        public Task AddAsync(UserAccount account, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PurgeStaleCredentialsAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
        public Task<bool> SaveRotationAsync(RefreshTokenSession rotatedFrom, CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class TokenIssuer : ITokenIssuer
    {
        public int IssueCount { get; private set; }
        public (string AccessToken, DateTime ExpiresAtUtc) Issue(UserAccount user, Guid sessionId)
        {
            IssueCount++;
            return ($"access:{sessionId}", Now.AddMinutes(15));
        }
    }

    private sealed class TokenProtector : ITokenProtector
    {
        public string Hash(string token) => "hash:" + token;
        public string GenerateToken() => Guid.NewGuid().ToString();
    }

    private sealed class PasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string hashedPassword, string providedPassword) => hashedPassword == Hash(providedPassword);
    }

    private sealed class TokenRevocation : ITokenRevocationService
    {
        public Task RevokeAllAccessTokensAsync(Guid userId, CancellationToken cancellationToken)
            => Task.CompletedTask;
        public Task RevokeSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class IdGenerator : IIdGenerator { public Guid New() => Guid.NewGuid(); }
    private sealed class Clock : IClock { public DateTime UtcNow => Now; }
}
