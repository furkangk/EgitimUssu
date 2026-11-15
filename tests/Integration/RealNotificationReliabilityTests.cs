using System.Data.Common;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Modules.Parents.Infrastructure;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Modules.Settings.Domain;
using EgitimUssu.Modules.Settings.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Push;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EgitimUssu.Tests.Integration;
[Collection("containers")]
public sealed class RealNotificationReliabilityTests(ContainerFixture fixture)
{
    private static readonly DateTime Now = new(2026,11,15,12,0,0,DateTimeKind.Utc);
    [SkippableFact]
    public async Task Failed_Hundred_Do_Not_Starve_Fresh_Push_And_Reads_Are_Constant()
    {
        Skip.IfNot(fixture.Available,"Docker gerekli.");
        await using var test=await Database.Create(fixture.PostgresConnectionString);
        var db=test.Notifications; var failedUser=Guid.NewGuid(); var freshUser=Guid.NewGuid();
        for(var i=0;i<100;i++) db.UserNotifications.Add(Row(failedUser,Now.AddHours(-1)));
        var fresh=Row(freshUser,Now); db.UserNotifications.Add(fresh);
        db.DeviceTokens.AddRange(Token(failedUser,"fail"),Token(freshUser,"ok")); await db.SaveChangesAsync();db.ChangeTracker.Clear();
        test.Reads.Commands.Clear();
        Assert.Equal(0,await test.Dispatch().DispatchPendingUserNotificationsAsync());
        Assert.Equal(3,test.Reads.Commands.Count); // Pending + Settings toplu + cihaz toplu
        test.Reads.Commands.Clear();
        Assert.Equal(1,await test.Dispatch().DispatchPendingUserNotificationsAsync());
        Assert.Equal(3,test.Reads.Commands.Count);
        db.ChangeTracker.Clear();
        Assert.Equal(100,await db.UserNotifications.CountAsync(x=>x.PushAttempts==1 && x.NextAttemptOnUtc==Now.AddSeconds(30)));
        Assert.Equal(NotificationPushStatus.Sent,(await db.UserNotifications.FindAsync(fresh.Id))!.PushStatus);
        Assert.Equal(101,test.Sender.Calls);
    }
    [SkippableFact]
    public async Task Expired_Lesson_And_Disabled_Push_Keep_InApp_Without_Sending()
    {
        Skip.IfNot(fixture.Available,"Docker gerekli.");
        await using var test=await Database.Create(fixture.PostgresConnectionString);
        var user=Guid.NewGuid(); var stale=Row(user,Now.AddHours(-2),UserNotificationKind.LessonReminder);
        var disabled=Row(user,Now); test.Notifications.UserNotifications.AddRange(stale,disabled);
        test.Notifications.DeviceTokens.Add(Token(user,"ok"));
        test.Settings.UserSettings.Add(new UserSetting(Guid.NewGuid(),user,false,true,true,true,true,true,true,true,
            PrivacyLevel.Standard,SessionTerminationPolicy.KeepLatest,Now));
        await test.Settings.SaveChangesAsync();await test.Notifications.SaveChangesAsync();
        Assert.Equal(0,await test.Dispatch().DispatchPendingUserNotificationsAsync());
        Assert.Equal(0,test.Sender.Calls);test.Notifications.ChangeTracker.Clear();
        var stored=await test.Notifications.UserNotifications.FindAsync(stale.Id);
        Assert.Equal(NotificationPushStatus.Expired,stored!.PushStatus);Assert.Null(stored.PushedOnUtc);
        Assert.Equal(2,await test.Notifications.UserNotifications.CountAsync());
        Assert.All(await test.Notifications.UserNotifications.ToArrayAsync(),x=>Assert.Null(x.ReadOnUtc));
    }
    [SkippableFact]
    public async Task Paged_Weekly_Summary_Handles_205_Children_And_Local_New_Week_Exactly_Once()
    {
        Skip.IfNot(fixture.Available,"Docker gerekli.");
        await using var test=await Database.Create(fixture.PostgresConnectionString);
        var parent=Guid.NewGuid(); test.Parents.ParentProfiles.Add(new ParentProfile(Guid.NewGuid(),parent,"Veli",null,null,Now));
        for(var i=0;i<205;i++)
        {
            var link=new ParentChildLink(Guid.NewGuid(),parent,Guid.NewGuid(),"Çocuk",null,null,true,Now);
            Assert.True(link.Approve(parent,null,Now).IsSuccess);test.Parents.ParentChildLinks.Add(link);
        }
        await test.Parents.SaveChangesAsync();test.Parents.ChangeTracker.Clear();test.Reads.Commands.Clear();
        var processor=new ParentWeeklySummaryProcessor(test.Services.GetRequiredService<IParentNotificationDirectory>(),
            test.Services.GetRequiredService<IParentNotificationRepository>(),new GuidIdGenerator(),new LocalCalendar("Europe/Istanbul",test.Clock));
        Assert.Equal(205,await processor.RunAsync(Now));
        Assert.Equal(4,test.Reads.Commands.Count); // iki sayfa + iki toplu dedup (haklar bu testte sahte)
        test.Reads.Commands.Clear();
        Assert.Equal(0,await processor.RunAsync(Now.AddHours(1)));
        Assert.Equal(4,test.Reads.Commands.Count);
        Assert.Equal(205,await test.Notifications.ParentNotifications.CountAsync());
        Assert.Equal(205,await processor.RunAsync(new DateTime(2026,11,15,21,30,0,DateTimeKind.Utc)));
        Assert.Equal(410,await test.Notifications.ParentNotifications.CountAsync());
    }
    [SkippableFact]
    public async Task Daily_Snapshot_Cycle_Persists_Missed_Goals_In_Batches()
    {
        Skip.IfNot(fixture.Available,"Docker gerekli.");
        await using var test=await Database.Create(fixture.PostgresConnectionString);
        await using var db=new ProgressTrackingDbContext(new DbContextOptionsBuilder<ProgressTrackingDbContext>()
            .UseNpgsql(test.ConnectionString,o=>o.MigrationsHistoryTable("__ef_migrations_history","progress_tracking")).Options,new NoOpDomainEventMapper());
        await db.Database.MigrateAsync();
        for(var i=0;i<205;i++) db.TopicGoals.Add(new TopicGoal(Guid.NewGuid(),Guid.NewGuid(),"Mat","Konu",MasteryLevel.Mastered,null,
            Guid.NewGuid(),TopicGoalSetterRole.Student,new DateOnly(2026,11,15),Now));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var clock=new Clock { UtcNow = new DateTime(2026,11,15,21,30,0,DateTimeKind.Utc) };
        var processor=new ProgressSnapshotProcessor(db,clock,new GuidIdGenerator(),new LocalCalendar("Europe/Istanbul",clock));
        Assert.Equal(0,await processor.RunAsync());Assert.Equal(0,await processor.RunAsync());
        Assert.Equal(205,await db.TopicGoals.CountAsync(x=>x.Status==TopicGoalStatus.Missed));
    }
    [SkippableFact]
    public async Task Migration_Backfills_Expiry_And_Sent_Status_And_Scrubs_Old_Message_Text_Up_Down_Up()
    {
        Skip.IfNot(fixture.Available,"Docker gerekli.");
        await using var test=await Database.Create(fixture.PostgresConnectionString);
        var db=test.Notifications;
        var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var target=Assert.Single(migrations,x=>x.EndsWith("_NotificationDeliveryBackoff",StringComparison.Ordinal));
        var before=migrations[Array.IndexOf(migrations,target)-1]; var migrator=db.GetService<IMigrator>();
        await migrator.MigrateAsync(before);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO notifications.user_notifications ("Id","UserId","Kind","Title","Body","CreatedOnUtc","PushedOnUtc") VALUES
            ('a1906000-0000-4000-8000-000000000001','a1906000-0000-4000-8000-000000000010','MessageReceived','Mesaj','silinmiş özel içerik','2026-11-15T12:00:00Z',NULL),
            ('a1906000-0000-4000-8000-000000000002','a1906000-0000-4000-8000-000000000010','LessonReminder','Ders','başlıyor','2026-11-15T10:00:00Z',NULL),
            ('a1906000-0000-4000-8000-000000000003','a1906000-0000-4000-8000-000000000010','MessageReceived','Mesaj','ikinci özel içerik','2026-11-15T12:00:00Z','2026-11-15T12:01:00Z');
            """);
        for(var i=0;i<2;i++)
        {
            await migrator.MigrateAsync(target);db.ChangeTracker.Clear();
            var rows=await db.UserNotifications.OrderBy(x=>x.Id).ToArrayAsync();
            Assert.Equal(3,rows.Length);Assert.Equal("Bir kullanıcı size mesaj gönderdi.",rows[0].Body);
            Assert.Equal("Bir kullanıcı size mesaj gönderdi.",rows[2].Body);
            Assert.Equal(NotificationPushStatus.Pending,rows[0].PushStatus);
            Assert.Equal(NotificationPushStatus.Sent,rows[2].PushStatus);
            Assert.Equal(Now.AddDays(1),rows[0].ExpiresOnUtc);
            Assert.Equal(Now.AddMinutes(-90),rows[1].ExpiresOnUtc);
            Assert.All(rows,x=>Assert.Equal(0,x.PushAttempts));
            if(i==0) await migrator.MigrateAsync(before);
        }
    }
    private static UserNotification Row(Guid user,DateTime time,UserNotificationKind kind=UserNotificationKind.System)
        =>new(Guid.NewGuid(),user,kind,"Başlık","Gövde","/route",time);
    private static DeviceToken Token(Guid user,string token)=>new(Guid.NewGuid(),user,token,DevicePlatform.Android,null,Now);
    private sealed class Clock:IClock {public DateTime UtcNow{get;set;}=Now;}
    private sealed class Sender:IPushSender
    {
        public int Calls{get;private set;}
        public Task<PushResult> SendAsync(string deviceToken,PushMessage message,CancellationToken cancellationToken=default)
        {Calls++;return Task.FromResult(new PushResult(deviceToken=="fail"?PushDeliveryStatus.TransientFailure:PushDeliveryStatus.Delivered,null));}
    }
    private sealed class Entitlements:IEntitlementDirectory
    {
        private static readonly Entitlement Premium=new(MembershipTier.Premium,null,int.MaxValue,false,true,true,true);
        public Task<Entitlement> GetAsync(Guid userId,CancellationToken ct)=>Task.FromResult(Premium);
        public Task<IReadOnlyDictionary<Guid,Entitlement>> GetManyAsync(IReadOnlyCollection<Guid> userIds,CancellationToken ct)
            =>Task.FromResult<IReadOnlyDictionary<Guid,Entitlement>>(userIds.Distinct().ToDictionary(x=>x,_=>Premium));
    }
    private sealed class Reads:DbCommandInterceptor
    {
        public List<string> Commands{get;}=[];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,
            InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {if(command.CommandText.StartsWith("SELECT",StringComparison.Ordinal))Commands.Add(command.CommandText);return ValueTask.FromResult(result);}
    }
    private sealed class Database(string adminConnection,string name,string connectionString,ServiceProvider services,
        NotificationsDbContext notifications,SettingsDbContext settings,ParentsDbContext parents,Reads reads,Clock clock):IAsyncDisposable
    {
        public string ConnectionString{get;}=connectionString;
        public ServiceProvider Services{get;}=services;
        public NotificationsDbContext Notifications{get;}=notifications;
        public SettingsDbContext Settings{get;}=settings;
        public ParentsDbContext Parents{get;}=parents;
        public Reads Reads{get;}=reads;
        public Clock Clock{get;}=clock;
        public Sender Sender{get;}=new();
        public NotificationDispatchProcessor Dispatch()=>new(Services.GetRequiredService<ILessonReminderRepository>(),Services.GetRequiredService<IDeviceTokenRepository>(),
            Services.GetRequiredService<IUserNotificationRepository>(),Sender,Services.GetRequiredService<IUserNotificationPreferences>(),Clock,null!,null!,null!);
        public static async Task<Database> Create(string connection)
        {
            var name="notification_reliability_"+Guid.NewGuid().ToString("N");
            await using var admin=new NpgsqlConnection(connection);await admin.OpenAsync();
            await using(var create=new NpgsqlCommand($"CREATE DATABASE {name}",admin))await create.ExecuteNonQueryAsync();
            var isolated=new NpgsqlConnectionStringBuilder(connection){Database=name,Pooling=false}.ConnectionString;
            var reads=new Reads();var clock=new Clock();var mapper=new NoOpDomainEventMapper();
            var notifications=new NotificationsDbContext(new DbContextOptionsBuilder<NotificationsDbContext>().UseNpgsql(isolated,
                o=>o.MigrationsHistoryTable("__ef_migrations_history","notifications")).AddInterceptors(reads).Options,mapper);
            var settings=new SettingsDbContext(new DbContextOptionsBuilder<SettingsDbContext>().UseNpgsql(isolated,
                o=>o.MigrationsHistoryTable("__ef_migrations_history","settings")).AddInterceptors(reads).Options,mapper);
            var parents=new ParentsDbContext(new DbContextOptionsBuilder<ParentsDbContext>().UseNpgsql(isolated,
                o=>o.MigrationsHistoryTable("__ef_migrations_history","parents")).AddInterceptors(reads).Options,mapper);
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:Postgres"]=isolated}).Build();
            var registration=new ServiceCollection();registration.AddNotificationsModule(config);registration.AddSettingsModule(config);registration.AddParentsModule(config);
            registration.AddSingleton(notifications);registration.AddSingleton(settings);registration.AddSingleton(parents);registration.AddSingleton<IEntitlementDirectory>(new Entitlements());
            var test=new Database(connection,name,isolated,registration.BuildServiceProvider(),notifications,settings,parents,reads,clock);
            try{await notifications.Database.MigrateAsync();await settings.Database.MigrateAsync();await parents.Database.MigrateAsync();return test;}
            catch{await test.DisposeAsync();throw;}
        }
        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();await Notifications.DisposeAsync();await Settings.DisposeAsync();await Parents.DisposeAsync();
            await using var admin=new NpgsqlConnection(adminConnection);await admin.OpenAsync();
            await using var drop=new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
        }
    }
}
