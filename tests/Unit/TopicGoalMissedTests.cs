using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Modules.ProgressTracking.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;
public sealed class TopicGoalMissedTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 21, 30, 0, DateTimeKind.Utc); // TR 16 Kasım
    [Fact]
    public void Deadline_Day_Remains_Active_Then_Missed_Is_Terminal()
    {
        var goal = New(new DateOnly(2026,11,15));
        goal.MarkMissed(new DateOnly(2026,11,15));
        Assert.Equal(TopicGoalStatus.Active, goal.Status);
        goal.MarkMissed(new DateOnly(2026,11,16));
        Assert.Equal(TopicGoalStatus.Missed, goal.Status);
        goal.MarkAchieved(Now);
        Assert.Equal(TopicGoalStatus.Missed, goal.Status);
        Assert.Null(goal.AchievedOnUtc);
        Assert.Empty(goal.DomainEvents);
    }
    [Fact]
    public async Task Existing_Snapshot_Cycle_Expires_Only_Past_Active_Goals_In_Local_Day()
    {
        await using var db = new ProgressTrackingDbContext(new DbContextOptionsBuilder<ProgressTrackingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new NoOpDomainEventMapper());
        var expired=New(new DateOnly(2026,11,15)); var today=New(new DateOnly(2026,11,16));
        var timeless=New(null); var achieved=New(new DateOnly(2026,11,14)); achieved.MarkAchieved(Now.AddDays(-1));
        var cancelled=New(new DateOnly(2026,11,14)); cancelled.Cancel();
        db.TopicGoals.AddRange(expired,today,timeless,achieved,cancelled); await db.SaveChangesAsync();
        var clock=new MessagingClock(Now);
        var processor = new ProgressSnapshotProcessor(db,clock,new MessagingIdGenerator(),new LocalCalendar("Europe/Istanbul",clock));
        await processor.RunAsync(); await processor.RunAsync();
        Assert.Equal(TopicGoalStatus.Missed,expired.Status);
        Assert.Equal(TopicGoalStatus.Active,today.Status); Assert.Equal(TopicGoalStatus.Active,timeless.Status);
        Assert.Equal(TopicGoalStatus.Achieved,achieved.Status); Assert.Equal(TopicGoalStatus.Cancelled,cancelled.Status);
    }
    [Fact]
    public async Task Late_Success_Before_Daily_Scan_Cannot_Rescue_An_Expired_Goal()
    {
        var clock = new MessagingClock(Now);
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventMapper>(new NoOpDomainEventMapper());
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IIdGenerator>(new MessagingIdGenerator());
        services.AddSingleton<ILocalCalendar>(new LocalCalendar("Europe/Istanbul", clock));
        services.AddProgressTrackingModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["ConnectionStrings:Postgres"] = $"InMemory:{Guid.NewGuid()}" }).Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
        var goal = new TopicGoal(Guid.NewGuid(), Guid.NewGuid(), "Mat", "Konu", MasteryLevel.NotStarted, null,
            Guid.NewGuid(), TopicGoalSetterRole.Student, new DateOnly(2026,11,15), Now.AddDays(-2));
        db.TopicGoals.Add(goal); await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<MasteryService>().ApplyStudyAsync(goal.StudentId, "Mat", "Konu", 10, default);
        Assert.Equal(TopicGoalStatus.Missed, goal.Status);
        Assert.Null(goal.AchievedOnUtc);
    }

    private static TopicGoal New(DateOnly? date)=>new(Guid.NewGuid(),Guid.NewGuid(),"Mat","Konu",MasteryLevel.Mastered,null,
        Guid.NewGuid(),TopicGoalSetterRole.Student,date,Now.AddDays(-10));
}
