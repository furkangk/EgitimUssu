using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P03 Task 4 (M11-3/M11-4): Rolden bağımsız in-app bildirim merkezi.
/// Kritik davranışlar: okundu işareti idempotent, rozet sayısı yalnız okunmamışları sayar,
/// kullanıcı yalnız kendi bildirimini okur (varsayılan-deny).
/// </summary>
public sealed class UserNotificationTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkRead_Should_Set_ReadOn_Once()
    {
        var notification = CreateNotification(Guid.NewGuid());

        Assert.False(notification.IsRead);

        notification.MarkRead(Now.AddMinutes(1));
        Assert.True(notification.IsRead);
        Assert.Equal(Now.AddMinutes(1), notification.ReadOnUtc);

        notification.MarkRead(Now.AddMinutes(5));
        Assert.Equal(Now.AddMinutes(1), notification.ReadOnUtc);
    }

    [Fact]
    public void MarkPushed_Should_Set_PushedOn_Once()
    {
        var notification = CreateNotification(Guid.NewGuid());

        Assert.Null(notification.PushedOnUtc);

        notification.MarkPushed(Now.AddMinutes(1));
        notification.MarkPushed(Now.AddMinutes(5));

        Assert.Equal(Now.AddMinutes(1), notification.PushedOnUtc);
    }

    [Fact]
    public async Task List_Should_Return_Newest_First_With_Total_Count()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeUserNotificationRepository(
            CreateNotification(userId, createdOnUtc: Now.AddMinutes(-30)),
            CreateNotification(userId, createdOnUtc: Now.AddMinutes(-10)),
            CreateNotification(Guid.NewGuid()));
        var handler = new ListUserNotificationsQueryHandler(repository);

        var result = await handler.Handle(new ListUserNotificationsQuery(userId, OnlyUnread: false, Skip: 0, Take: 10), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal(Now.AddMinutes(-10), result.Value.Items.First().CreatedOnUtc);
    }

    [Fact]
    public async Task List_OnlyUnread_Should_Exclude_Read_Notifications()
    {
        var userId = Guid.NewGuid();
        var read = CreateNotification(userId);
        read.MarkRead(Now);
        var repository = new FakeUserNotificationRepository(read, CreateNotification(userId));
        var handler = new ListUserNotificationsQueryHandler(repository);

        var result = await handler.Handle(new ListUserNotificationsQuery(userId, OnlyUnread: true, Skip: 0, Take: 10), default);

        Assert.Equal(1, result.Value!.TotalCount);
    }

    [Fact]
    public async Task Unread_Count_Should_Count_Only_Unread()
    {
        var userId = Guid.NewGuid();
        var read = CreateNotification(userId);
        read.MarkRead(Now);
        var repository = new FakeUserNotificationRepository(read, CreateNotification(userId), CreateNotification(userId));
        var handler = new GetUnreadNotificationCountQueryHandler(repository);

        var result = await handler.Handle(new GetUnreadNotificationCountQuery(userId), default);

        Assert.Equal(2, result.Value);
    }

    [Fact]
    public async Task MarkRead_Command_Should_Fail_For_Unknown_Notification()
    {
        var handler = new MarkNotificationReadCommandHandler(
            new FakeUserNotificationRepository(),
            new FixedClock(Now));

        var result = await handler.Handle(new MarkNotificationReadCommand(Guid.NewGuid()), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("notifications.notification_not_found", result.Error.Code);
    }

    [Fact]
    public async Task MarkRead_Command_Should_Mark_Existing_Notification()
    {
        var notification = CreateNotification(Guid.NewGuid());
        var repository = new FakeUserNotificationRepository(notification);
        var handler = new MarkNotificationReadCommandHandler(repository, new FixedClock(Now));

        var result = await handler.Handle(new MarkNotificationReadCommand(notification.Id), default);

        Assert.True(result.IsSuccess);
        Assert.True(notification.IsRead);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(true, false, true)]   // sahibi
    [InlineData(false, true, true)]   // Admin
    [InlineData(false, false, false)] // yabancı
    public async Task Query_Authorizer_Should_Allow_Only_Self_Or_Admin(bool isSelf, bool isAdmin, bool expected)
    {
        var userId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser(
            isSelf ? userId : Guid.NewGuid(),
            isAdmin ? ["Admin"] : ["Student"]);
        var authorizer = new UserNotificationAuthorizer(currentUser, new FakeUserNotificationRepository());

        var result = await authorizer.Authorize(new ListUserNotificationsQuery(userId, false, 0, 20), default);

        Assert.Equal(expected, result.IsSuccess);
    }

    [Fact]
    public async Task Anonymous_Should_Be_Denied()
    {
        var authorizer = new UserNotificationAuthorizer(
            new FakeCurrentUser(null, []),
            new FakeUserNotificationRepository());

        var result = await authorizer.Authorize(new GetUnreadNotificationCountQuery(Guid.NewGuid()), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task MarkRead_Authorizer_Should_Deny_Other_Users_Notification()
    {
        var notification = CreateNotification(Guid.NewGuid());
        var authorizer = new UserNotificationAuthorizer(
            new FakeCurrentUser(Guid.NewGuid(), ["Student"]),
            new FakeUserNotificationRepository(notification));

        var result = await authorizer.Authorize(new MarkNotificationReadCommand(notification.Id), default);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task MarkRead_Authorizer_Should_Allow_Owner()
    {
        var userId = Guid.NewGuid();
        var notification = CreateNotification(userId);
        var authorizer = new UserNotificationAuthorizer(
            new FakeCurrentUser(userId, ["Student"]),
            new FakeUserNotificationRepository(notification));

        var result = await authorizer.Authorize(new MarkNotificationReadCommand(notification.Id), default);

        Assert.True(result.IsSuccess);
    }

    private static UserNotification CreateNotification(Guid userId, DateTime? createdOnUtc = null)
        => new(
            Guid.NewGuid(),
            userId,
            UserNotificationKind.AssignmentCreated,
            "Yeni ödev",
            "Matematik ödevi eklendi",
            "/student/assignments",
            createdOnUtc ?? Now);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeCurrentUser(Guid? userId, string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId?.ToString();

        public bool IsAuthenticated { get; } = userId.HasValue;

        public IReadOnlyCollection<string> Roles { get; } = roles;

        public string? Email => null;
    }

    internal sealed class FakeUserNotificationRepository(params UserNotification[] notifications) : IUserNotificationRepository
    {
        private readonly List<UserNotification> _items = [.. notifications];

        public int SaveChangesCallCount { get; private set; }

        public Task<(IReadOnlyList<UserNotification> Items, int TotalCount)> ListAsync(
            Guid userId,
            bool onlyUnread,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            var matching = _items
                .Where(n => n.UserId == userId && (!onlyUnread || !n.IsRead))
                .OrderByDescending(n => n.CreatedOnUtc)
                .ToList();

            return Task.FromResult<(IReadOnlyList<UserNotification>, int)>(
                (matching.Skip(skip).Take(take).ToList(), matching.Count));
        }

        public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(_items.Count(n => n.UserId == userId && !n.IsRead));

        public Task<UserNotification?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken)
            => Task.FromResult(_items.FirstOrDefault(n => n.Id == notificationId));

        public Task<IReadOnlyList<UserNotification>> ListPendingPushAsync(int maxCount, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UserNotification>>(
                _items.Where(n => n.PushedOnUtc is null).Take(maxCount).ToList());

        public Task AddAsync(UserNotification notification, CancellationToken cancellationToken)
        {
            _items.Add(notification);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }
}
