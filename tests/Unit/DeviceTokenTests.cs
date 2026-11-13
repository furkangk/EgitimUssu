using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class DeviceTokenTests
{
    private static readonly DateTime Now = new(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void New_Token_Should_Be_Active()
    {
        var token = new DeviceToken(Guid.NewGuid(), Guid.NewGuid(), "fcm-token", DevicePlatform.Android, "Pixel 8", Now);

        Assert.True(token.IsActive);
        Assert.Equal(Now, token.CreatedOnUtc);
        Assert.Equal(Now, token.LastSeenOnUtc);
    }

    [Fact]
    public void Touch_Should_Update_LastSeen_And_Reactivate()
    {
        var token = new DeviceToken(Guid.NewGuid(), Guid.NewGuid(), "fcm-token", DevicePlatform.IOS, null, Now);

        token.Deactivate(Now.AddMinutes(1));
        Assert.False(token.IsActive);

        token.Touch(Now.AddMinutes(2));
        Assert.True(token.IsActive);
        Assert.Equal(Now.AddMinutes(2), token.LastSeenOnUtc);
    }

    [Fact]
    public void Deactivate_Should_Clear_Active_And_Stamp_LastSeen()
    {
        var token = new DeviceToken(Guid.NewGuid(), Guid.NewGuid(), "fcm-token", DevicePlatform.Web, "Chrome", Now);

        token.Deactivate(Now.AddHours(3));

        Assert.False(token.IsActive);
        Assert.Equal(Now.AddHours(3), token.LastSeenOnUtc);
        Assert.Equal(Now, token.CreatedOnUtc);
    }
}

/// <summary>
/// M11-2: Cihaz token'ı kaydetme/silme davranışı. Kayıt idempotenttir (aynı token yeniden
/// gönderilirse yeni satır açılmaz), silme de idempotenttir (bilinmeyen token 200 döner).
/// </summary>
public sealed class DeviceTokenCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Register_Should_Add_New_Token()
    {
        var repository = new FakeDeviceTokenRepository();
        var userId = Guid.NewGuid();

        var result = await new RegisterDeviceTokenCommandHandler(repository, new SeqIdGenerator(), new FixedClock(Now))
            .Handle(new RegisterDeviceTokenCommand(userId, "token-1", DevicePlatform.Android, "Pixel 8"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(repository.Tokens);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal("token-1", stored.Token);
        Assert.True(stored.IsActive);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Register_Same_Token_Twice_Should_Touch_Not_Duplicate()
    {
        var repository = new FakeDeviceTokenRepository();
        var userId = Guid.NewGuid();
        var handler = new RegisterDeviceTokenCommandHandler(repository, new SeqIdGenerator(), new FixedClock(Now));
        await handler.Handle(new RegisterDeviceTokenCommand(userId, "token-1", DevicePlatform.Android, "Pixel 8"), CancellationToken.None);

        var later = new RegisterDeviceTokenCommandHandler(repository, new SeqIdGenerator(), new FixedClock(Now.AddDays(1)));
        var result = await later.Handle(
            new RegisterDeviceTokenCommand(userId, "token-1", DevicePlatform.Android, "Pixel 8"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(repository.Tokens);
        Assert.Equal(Now.AddDays(1), stored.LastSeenOnUtc);
    }

    [Fact]
    public async Task Register_Should_Reject_Empty_Token()
    {
        var repository = new FakeDeviceTokenRepository();

        var result = await new RegisterDeviceTokenCommandHandler(repository, new SeqIdGenerator(), new FixedClock(Now))
            .Handle(new RegisterDeviceTokenCommand(Guid.NewGuid(), "   ", DevicePlatform.IOS, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("notifications.invalid_device_token", result.Error.Code);
        Assert.Empty(repository.Tokens);
    }

    [Fact]
    public async Task Unregister_Should_Deactivate_Existing_Token()
    {
        var repository = new FakeDeviceTokenRepository();
        var userId = Guid.NewGuid();
        await new RegisterDeviceTokenCommandHandler(repository, new SeqIdGenerator(), new FixedClock(Now))
            .Handle(new RegisterDeviceTokenCommand(userId, "token-1", DevicePlatform.Android, null), CancellationToken.None);

        var result = await new UnregisterDeviceTokenCommandHandler(repository, new FixedClock(Now.AddHours(2)))
            .Handle(new UnregisterDeviceTokenCommand(userId, "token-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(Assert.Single(repository.Tokens).IsActive);
    }

    [Fact]
    public async Task Unregister_Unknown_Token_Should_Succeed_Idempotently()
    {
        var repository = new FakeDeviceTokenRepository();

        var result = await new UnregisterDeviceTokenCommandHandler(repository, new FixedClock(Now))
            .Handle(new UnregisterDeviceTokenCommand(Guid.NewGuid(), "bilinmeyen"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(repository.Tokens);
    }

    [Fact]
    public async Task Authorizer_Should_Allow_Self_And_Admin_But_Deny_Others()
    {
        var ownerId = Guid.NewGuid();
        var authorizer = new DeviceTokenCommandAuthorizer(new FakeCurrentUser(ownerId.ToString(), "Student"));
        var command = new RegisterDeviceTokenCommand(ownerId, "t", DevicePlatform.Android, null);

        Assert.True((await authorizer.Authorize(command, CancellationToken.None)).IsSuccess);

        var admin = new DeviceTokenCommandAuthorizer(new FakeCurrentUser(Guid.NewGuid().ToString(), "Admin"));
        Assert.True((await admin.Authorize(command, CancellationToken.None)).IsSuccess);

        var stranger = new DeviceTokenCommandAuthorizer(new FakeCurrentUser(Guid.NewGuid().ToString(), "Teacher"));
        var denied = await stranger.Authorize(command, CancellationToken.None);
        Assert.True(denied.IsFailure);
        Assert.Equal("shared.forbidden", denied.Error.Code);

        var anonymous = new DeviceTokenCommandAuthorizer(new FakeCurrentUser(null));
        Assert.True((await anonymous.Authorize(command, CancellationToken.None)).IsFailure);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class SeqIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FakeCurrentUser(string? userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsAuthenticated => UserId is not null;
    }

    private sealed class FakeDeviceTokenRepository : IDeviceTokenRepository
    {
        public List<DeviceToken> Tokens { get; } = [];
        public int SaveCount { get; private set; }

        public Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken)
            => Task.FromResult(Tokens.FirstOrDefault(t => t.Token == token));

        public Task<IReadOnlyList<DeviceToken>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DeviceToken>>(Tokens.Where(t => t.UserId == userId && t.IsActive).ToList());

        public Task AddAsync(DeviceToken deviceToken, CancellationToken cancellationToken)
        {
            Tokens.Add(deviceToken);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
