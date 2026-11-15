using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Unit;

public sealed class NotificationBackoffTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    internal static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventMapper>(new NoOpDomainEventMapper());
        services.AddNotificationsModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["ConnectionStrings:Postgres"] = $"InMemory:{Guid.NewGuid()}" }).Build());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Failure_Backs_Off_Exponentially_And_Stops_After_Eight_Attempts()
    {
        var row = New(Now);
        for (var i = 1; i <= 8; i++)
        {
            row.MarkPushFailed(Now);
            Assert.Equal(i, row.PushAttempts);
            Assert.Equal(i < 8 ? NotificationPushStatus.Pending : NotificationPushStatus.Failed, row.PushStatus);
            if (i < 8) Assert.Equal(Now.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, i - 1))), row.NextAttemptOnUtc);
        }
        row.MarkPushFailed(Now);
        Assert.Equal(8, row.PushAttempts);
        Assert.Null(row.PushedOnUtc);
    }

    [Theory]
    [InlineData(UserNotificationKind.LessonReminder, 30)]
    [InlineData(UserNotificationKind.MessageReceived, 1440)]
    public void Expiry_Preserves_InApp_And_Never_Claims_Delivery(UserNotificationKind kind, int minutes)
    {
        var row = New(Now, kind);
        Assert.Equal(Now.AddMinutes(minutes), row.ExpiresOnUtc);
        row.ExpirePush(Now.AddMinutes(minutes - 1));
        Assert.Equal(NotificationPushStatus.Pending, row.PushStatus);
        row.ExpirePush(Now.AddMinutes(minutes));
        Assert.Equal(NotificationPushStatus.Expired, row.PushStatus);
        Assert.Null(row.PushedOnUtc);
        Assert.False(row.IsRead);
    }

    [Fact]
    public async Task Real_Repository_Skips_One_Hundred_Delayed_Failures_And_Returns_New_Work()
    {
        using var services = Services();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        for (var i = 0; i < 100; i++)
        {
            var row = New(Now.AddHours(-1));
            row.MarkPushFailed(Now);
            db.UserNotifications.Add(row);
        }
        var fresh = New(Now);
        db.UserNotifications.Add(fresh);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var repo = scope.ServiceProvider.GetRequiredService<IUserNotificationRepository>();
        var due = await repo.ListPendingPushAsync(100, default, Now.AddSeconds(1));
        Assert.Equal(fresh.Id, Assert.Single(due).Id);
    }

    internal static UserNotification New(DateTime time, UserNotificationKind kind = UserNotificationKind.System)
        => new(Guid.NewGuid(), Guid.NewGuid(), kind, "Başlık", "Gövde", "/route", time);
}
