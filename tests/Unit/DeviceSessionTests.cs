using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P13 Task 7 / M01-4: Cihaz oturumları — listeleme ve iptal.
/// <para>
/// Dört kural pazarlık dışıdır: (1) kullanıcı <b>yalnız kendi</b> oturumlarını görür;
/// (2) başkasının oturumunu iptal edemez; (3) "diğerlerini kapat" <b>mevcut oturumu korur</b> —
/// aksi halde kullanıcı kendini de atar ve ekran anlamsızlaşır; (4) iptal edilen refresh token
/// bir daha yenilenemez.
/// </para>
/// </summary>
public sealed class DeviceSessionTests
{
    private static readonly DateTime Now = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    // --- Listeleme ----------------------------------------------------------------------

    /// <summary>Yalnız kendi oturumları; başkasınınki hiç görünmez.</summary>
    [Fact]
    public async Task Should_List_Only_Own_Active_Sessions()
    {
        await using var context = NewContext();
        var (me, mySession) = await SeedUserAsync(context, "ben@example.com", "iPhone");
        await SeedUserAsync(context, "baskasi@example.com", "Pixel");

        var result = await NewListHandler(context, me.Id, mySession.Id)
            .Handle(new GetDeviceSessionsQuery(), default);

        Assert.True(result.IsSuccess);
        var sessions = result.Value!;
        Assert.Single(sessions);
        Assert.Equal("iPhone", sessions.Single().DeviceName);
        Assert.True(sessions.Single().IsCurrent);
    }

    /// <summary>İptal edilmiş ve süresi geçmiş oturumlar listelenmez.</summary>
    [Fact]
    public async Task Should_Hide_Revoked_And_Expired_Sessions()
    {
        await using var context = NewContext();
        var (user, current) = await SeedUserAsync(context, "ben@example.com", "iPhone");

        var revoked = NewSession(user.Id, "Eski telefon", Now.AddDays(-10));
        revoked.Revoke(Now.AddDays(-5), SessionRevocationReason.UserLogout);
        var expired = NewSession(user.Id, "Tablet", Now.AddDays(-40), expiresOnUtc: Now.AddDays(-10));
        user.RefreshSessions.Add(revoked);
        user.RefreshSessions.Add(expired);
        await context.SaveChangesAsync();

        var result = await NewListHandler(context, user.Id, current.Id)
            .Handle(new GetDeviceSessionsQuery(), default);

        Assert.Single(result.Value!);
        Assert.Equal("iPhone", result.Value!.Single().DeviceName);
    }

    // --- Tekil iptal --------------------------------------------------------------------

    [Fact]
    public async Task Should_Revoke_Own_Session()
    {
        await using var context = NewContext();
        var (user, current) = await SeedUserAsync(context, "ben@example.com", "iPhone");
        var other = NewSession(user.Id, "Tablet", Now.AddDays(-1));
        user.RefreshSessions.Add(other);
        await context.SaveChangesAsync();

        var result = await NewRevokeHandler(context, user.Id, current.Id)
            .Handle(new RevokeDeviceSessionCommand(other.Id), default);

        Assert.True(result.IsSuccess);
        var reloaded = await context.UserAccounts.Include(u => u.RefreshSessions)
            .SingleAsync(u => u.Id == user.Id);
        Assert.False(reloaded.RefreshSessions.Single(s => s.Id == other.Id).IsActive(Now));
        Assert.True(reloaded.RefreshSessions.Single(s => s.Id == current.Id).IsActive(Now));
    }

    /// <summary>Başkasının oturumu iptal edilemez — <b>403</b>, "bulunamadı" değil.</summary>
    [Fact]
    public async Task Should_Not_Revoke_Another_Users_Session()
    {
        await using var context = NewContext();
        var (me, myCurrent) = await SeedUserAsync(context, "ben@example.com", "iPhone");
        var (_, theirSession) = await SeedUserAsync(context, "baskasi@example.com", "Pixel");

        var result = await NewRevokeHandler(context, me.Id, myCurrent.Id)
            .Handle(new RevokeDeviceSessionCommand(theirSession.Id), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("shared.forbidden", result.Error.Code);

        var reloaded = await context.RefreshTokenSessions.SingleAsync(s => s.Id == theirSession.Id);
        Assert.True(reloaded.IsActive(Now));   // dokunulmamış olmalı
    }

    // --- Toplu iptal --------------------------------------------------------------------

    /// <summary>"Diğer tüm oturumları kapat" mevcut oturumu <b>korur</b>.</summary>
    [Fact]
    public async Task RevokeAll_Should_Keep_The_Current_Session()
    {
        await using var context = NewContext();
        var (user, current) = await SeedUserAsync(context, "ben@example.com", "iPhone");
        user.RefreshSessions.Add(NewSession(user.Id, "Tablet", Now.AddDays(-1)));
        user.RefreshSessions.Add(NewSession(user.Id, "Dizüstü", Now.AddDays(-2)));
        await context.SaveChangesAsync();

        var result = await NewRevokeAllHandler(context, user.Id, current.Id)
            .Handle(new RevokeAllDeviceSessionsCommand(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);   // kapatılan oturum sayısı

        var reloaded = await context.UserAccounts.Include(u => u.RefreshSessions)
            .SingleAsync(u => u.Id == user.Id);
        Assert.Single(reloaded.RefreshSessions, s => s.IsActive(Now));
        Assert.Equal(current.Id, reloaded.RefreshSessions.Single(s => s.IsActive(Now)).Id);
    }

    /// <summary>
    /// Jetonda oturum kimliği yoksa toplu iptal <b>reddedilir</b>: hangi oturumun korunacağı
    /// bilinemez ve "diğerlerini kapat" sessizce "hepsini kapat"a dönüşürdü.
    /// </summary>
    [Fact]
    public async Task RevokeAll_Should_Fail_Closed_When_Session_Is_Unknown()
    {
        await using var context = NewContext();
        var (user, _) = await SeedUserAsync(context, "ben@example.com", "iPhone");

        var result = await NewRevokeAllHandler(context, user.Id, sessionId: null)
            .Handle(new RevokeAllDeviceSessionsCommand(), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("identity.unknown_session", result.Error.Code);
        Assert.True((await context.RefreshTokenSessions.SingleAsync()).IsActive(Now));
    }

    // --- İptal edilen token yenilenemez --------------------------------------------------

    /// <summary>İptal edilen oturumun refresh token'ı bir daha kullanılamaz.</summary>
    [Fact]
    public void Revoked_Session_Should_Not_Be_Active()
    {
        var session = NewSession(Guid.NewGuid(), "iPhone", Now.AddDays(-1));

        Assert.True(session.IsActive(Now));
        session.Revoke(Now, SessionRevocationReason.DeviceRevoked);
        Assert.False(session.IsActive(Now));
        Assert.False(session.IsActive(Now.AddMinutes(1)));
    }

    /// <summary>
    /// M01-4: Jeton yenilendiğinde oturumun <b>başlangıç anı korunur</b>, "son kullanım" ilerler.
    /// Aksi halde 30 gün boyunca her yenilemede oturum "az önce açılmış" görünür ve kullanıcı
    /// cihazı ne zaman bağladığını asla göremez.
    /// </summary>
    [Fact]
    public void Rotated_Session_Should_Keep_Original_Start_And_Advance_Last_Use()
    {
        var original = NewSession(Guid.NewGuid(), "iPhone", Now.AddDays(-10));

        var rotated = new RefreshTokenSession(
            Guid.NewGuid(), original.UserAccountId, "yeni-hash", original.DeviceName,
            original.CreatedOnUtc, Now.AddDays(30), lastUsedOnUtc: Now);

        Assert.Equal(Now.AddDays(-10), rotated.CreatedOnUtc);
        Assert.Equal(Now, rotated.LastUsedOnUtc);
    }

    // --- Yardımcılar ---------------------------------------------------------------------

    private static IdentityDbContext NewContext() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new JsonDomainEventMapper());

    private static RefreshTokenSession NewSession(
        Guid userId, string? deviceName, DateTime createdOnUtc, DateTime? expiresOnUtc = null) =>
        new(Guid.NewGuid(), userId, Guid.NewGuid().ToString("N"), deviceName,
            createdOnUtc, expiresOnUtc ?? createdOnUtc.AddDays(30));

    private static async Task<(UserAccount User, RefreshTokenSession Session)> SeedUserAsync(
        IdentityDbContext context, string email, string deviceName)
    {
        var user = new UserAccount(
            Guid.NewGuid(), email, email.ToUpperInvariant(), "hash", "Ad", "Soyad", null,
            UserAccountStatus.Active, true, false, Now.AddDays(-30));
        var session = NewSession(user.Id, deviceName, Now.AddDays(-3));
        user.RefreshSessions.Add(session);
        context.UserAccounts.Add(user);
        await context.SaveChangesAsync();
        return (user, session);
    }

    private static GetDeviceSessionsQueryHandler NewListHandler(
        IdentityDbContext context, Guid userId, Guid? sessionId) =>
        new(new FakeUserAccountRepository(context), new FakeCurrentUser(userId, sessionId), new FixedClock(Now));

    private static RevokeDeviceSessionCommandHandler NewRevokeHandler(
        IdentityDbContext context, Guid userId, Guid? sessionId) =>
        new(new FakeUserAccountRepository(context), new FakeCurrentUser(userId, sessionId), new NoopTokenRevocation(), new FixedClock(Now));

    private static RevokeAllDeviceSessionsCommandHandler NewRevokeAllHandler(
        IdentityDbContext context, Guid userId, Guid? sessionId) =>
        new(new FakeUserAccountRepository(context), new FakeCurrentUser(userId, sessionId), new NoopTokenRevocation(), new FixedClock(Now));

    private sealed class NoopTokenRevocation : ITokenRevocationService
    {
        public Task RevokeAllAccessTokensAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    /// <summary>Üretimdeki repository <c>internal</c>; handler'lar zaten arayüze bağlı.</summary>
    private sealed class FakeUserAccountRepository(IdentityDbContext context) : IUserAccountRepository
    {
        public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
            => context.UserAccounts.Include(user => user.RefreshSessions)
                .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        public Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => context.UserAccounts.Include(user => user.RefreshSessions)
                .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

        public Task<UserAccount?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken)
            => context.UserAccounts.Include(user => user.RefreshSessions)
                .FirstOrDefaultAsync(
                    user => user.RefreshSessions.Any(session => session.RefreshTokenHash == refreshTokenHash),
                    cancellationToken);

        public async Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken)
            => await context.UserAccounts.AddAsync(userAccount, cancellationToken);

        public Task PurgeStaleCredentialsAsync(Guid userId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => context.SaveChangesAsync(cancellationToken);

        public async Task<bool> SaveRotationAsync(RefreshTokenSession rotatedFrom, CancellationToken cancellationToken)
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    private sealed class FakeCurrentUser(Guid userId, Guid? sessionId) : ICurrentUser
    {
        public string? UserId => userId.ToString();

        public string? Email => "ben@example.com";

        public IReadOnlyCollection<string> Roles => ["Teacher"];

        public bool IsAuthenticated => true;

        public Guid? SessionId => sessionId;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
