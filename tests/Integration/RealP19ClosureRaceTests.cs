using System.Data.Common;
using System.Text.Json;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Modules.Parents.Infrastructure;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealP19ClosureRaceTests(ContainerFixture fixture)
{
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mock_component_write_and_outbox_survive_transient_commit_failure(bool delete)
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infra = RealInfrastructure.Use(fixture);
        var fault = new CommitFailure();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.ConfigureDbContext<StudyDbContext>(o => o.AddInterceptors(fault))));
        var student = Guid.NewGuid(); var id = Guid.NewGuid(); var examId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
            var test = new TestResult(id, student, "Mat", "Türev", null, TestType.Subject, 20, 16, 0, 4, 4, null, now, true, true, now);
            var exam = new MockExam(examId, student, "TYT", now, now); exam.AddSubject(test);
            db.AddRange(test, exam); await db.SaveChangesAsync();
        }
        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
                var test = await db.TestResults.SingleAsync(x => x.Id == id);
                if (delete) { test.NotifyDeleted(); db.Remove(test); }
                else test.Edit("Mat", "Türev", null, TestType.Subject, 20, 4, 0, 16, 4, null, now, now);
                fault.Armed = true;
                await scope.ServiceProvider.GetRequiredService<IStudyRepository>().SaveChangesAsync(default);
            }
            Assert.Equal(1, fault.Failures);
            await using var read = factory.Services.CreateAsyncScope();
            var study = read.ServiceProvider.GetRequiredService<StudyDbContext>();
            var row = await study.TestResults.SingleOrDefaultAsync(x => x.Id == id);
            if (delete) Assert.Null(row); else Assert.Equal(4m, row!.Net);
            Assert.Equal(delete ? 0m : 4m, (await study.MockExams.SingleAsync(x => x.Id == examId)).TotalNet);
            Assert.Equal(1, await study.OutboxMessages.CountAsync(x => x.Payload.Contains(student.ToString())
                && x.Type == (delete ? "TestResultDeletedDomainEvent" : "TestResultEditedDomainEvent")));
        }
        finally
        {
            await using var clean = factory.Services.CreateAsyncScope();
            await clean.ServiceProvider.GetRequiredService<StudyDbContext>().OutboxMessages
                .Where(x => x.Payload.Contains(student.ToString())).ExecuteDeleteAsync();
        }
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expiry_scan_does_not_overwrite_concurrent_cancel_or_achievement(bool achieved)
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infra = RealInfrastructure.Use(fixture);
        var hook = new AfterRead("topic_goals");
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.ConfigureDbContext<ProgressTrackingDbContext>(o => o.AddInterceptors(hook))));
        var id = Guid.NewGuid(); var student = Guid.NewGuid();
        var now = factory.Services.GetRequiredService<IClock>().UtcNow;
        var calendar = factory.Services.GetRequiredService<ILocalCalendar>();
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
            db.Add(new TopicGoal(id, student, "Mat", "Türev", MasteryLevel.Mastered, null, student,
                TopicGoalSetterRole.Student, calendar.ToLocalDate(now).AddDays(-1), now));
            await db.SaveChangesAsync();
        }
        hook.Callback = async () =>
        {
            await using var other = factory.Services.CreateAsyncScope();
            var db = other.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
            var goal = await db.TopicGoals.SingleAsync(x => x.Id == id);
            if (achieved) goal.MarkAchieved(now); else goal.Cancel();
            await db.SaveChangesAsync();
        };
        await using (var scan = factory.Services.CreateAsyncScope())
            await scan.ServiceProvider.GetRequiredService<IProgressSnapshotProcessor>().RunAsync();
        Assert.True(hook.Ran);
        await using var read = factory.Services.CreateAsyncScope();
        var progress = read.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
        Assert.Equal(achieved ? TopicGoalStatus.Achieved : TopicGoalStatus.Cancelled,
            (await progress.TopicGoals.SingleAsync(x => x.Id == id)).Status);
        await progress.OutboxMessages.Where(x => x.Payload.Contains(student.ToString())).ExecuteDeleteAsync();
        await progress.TopicGoals.Where(x => x.Id == id).ExecuteDeleteAsync();
    }

    [SkippableFact]
    public async Task Merge_and_weekly_processor_cannot_leave_two_canonical_summaries()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infra = RealInfrastructure.Use(fixture);
        var from = Guid.NewGuid(); var to = Guid.NewGuid(); var parent = Guid.NewGuid();
        var hook = new BeforeMutation();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.ConfigureDbContext<NotificationsDbContext>(o => o.AddInterceptors(hook));
        }));
        var now = factory.Services.GetRequiredService<IClock>().UtcNow;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            db.Add(new ParentNotification(Guid.NewGuid(), parent, from, ParentNotificationType.WeeklySummary, "Özet", "hazır", now));
            await db.SaveChangesAsync();
        }
        Task<int>? concurrent = null;
        await using var summaryScope = factory.Services.CreateAsyncScope();
        hook.Callback = async () =>
        {
            concurrent = new ParentWeeklySummaryProcessor(new TargetDirectory(parent, to),
                summaryScope.ServiceProvider.GetRequiredService<IParentNotificationRepository>(),
                factory.Services.GetRequiredService<IIdGenerator>(), factory.Services.GetRequiredService<ILocalCalendar>()).RunAsync(now);
            // Eski kod burada tamamlanır; ortak kilit varsa birleşim commit'ini bekler.
            try { Assert.Equal(1, await concurrent.WaitAsync(TimeSpan.FromSeconds(1))); }
            catch (TimeoutException) { }
        };
        await using (var merge = factory.Services.CreateAsyncScope())
            await new NotificationsStudentMergedHandler(merge.ServiceProvider.GetRequiredService<NotificationsDbContext>(),
                factory.Services.GetRequiredService<IClock>(), factory.Services.GetRequiredService<ILocalCalendar>())
                .HandleAsync(Event("Students", "StudentProfilesMergedDomainEvent", new StudentProfilesMergedIntegrationEvent(from, to), now));
        Assert.True(hook.Ran); Assert.NotNull(concurrent); await concurrent.WaitAsync(TimeSpan.FromSeconds(15));
        await using var read = factory.Services.CreateAsyncScope();
        var notifications = read.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        Assert.Equal(1, await notifications.ParentNotifications.CountAsync(x => x.StudentId == to && x.ParentUserId == parent));
        Assert.Equal(0, await notifications.ParentNotifications.CountAsync(x => x.StudentId == from));
    }

    [SkippableFact]
    public async Task Parents_merge_retry_reconciles_counts_after_canonical_assignment_event()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infra = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var from = Guid.NewGuid(); var to = Guid.NewGuid(); var now = DateTime.UtcNow;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var parents = seed.ServiceProvider.GetRequiredService<ParentsDbContext>();
            var source = new ChildProgressSnapshot(Guid.NewGuid(), from, now);
            source.RegisterAssignmentCreated(now); source.RegisterAssignmentCreated(now);
            var target = new ChildProgressSnapshot(Guid.NewGuid(), to, now); target.RegisterAssignmentCreated(now);
            parents.AddRange(source, target); await parents.SaveChangesAsync();
            // Assignments ayağı tamamlanmış, Parents inbox henüz yazılamamış birleşim durumu.
            var assignments = seed.ServiceProvider.GetRequiredService<AssignmentsDbContext>();
            for (var i = 0; i < 3; i++) assignments.Add(new Assignment(Guid.NewGuid(), to, Guid.NewGuid(), null,
                "Ödev", null, null, AssignmentStatus.Pending, null, now, null));
            await assignments.SaveChangesAsync();
            await assignments.OutboxMessages.Where(x => x.Payload.Contains(to.ToString())).ExecuteDeleteAsync();
        }
        await HandleOnly(factory, "ParentAssignmentProjectionHandler", Event("Assignments", "AssignmentCreatedDomainEvent", new { studentId = to }, now));
        var message = Event("Students", "StudentProfilesMergedDomainEvent", new StudentProfilesMergedIntegrationEvent(from, to), now);
        await HandleOnly(factory, "StudentProfilesMergedParentsHandler", message);
        await HandleOnly(factory, "StudentProfilesMergedParentsHandler", message);
        await using var read = factory.Services.CreateAsyncScope();
        var db = read.ServiceProvider.GetRequiredService<ParentsDbContext>();
        var snapshot = await db.ChildProgressSnapshots.SingleAsync(x => x.StudentId == to);
        Assert.Equal((3, 3, 0), (snapshot.TotalAssignmentCount, snapshot.OpenAssignmentCount, snapshot.CompletedAssignmentCount));
        Assert.False(await db.ChildProgressSnapshots.AnyAsync(x => x.StudentId == from));
    }

    private static async Task HandleOnly(WebApplicationFactory<Program> factory, string name, IntegrationEvent message)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetServices<IIntegrationEventHandler>().Single(x => x.GetType().Name == name).HandleAsync(message);
    }
    private static IntegrationEvent Event(string module, string name, object payload, DateTime now)
        => new(Guid.NewGuid(), now, name, module, JsonSerializer.Serialize(payload, IntegrationEventSerialization.Options));

    private sealed class CommitFailure : DbTransactionInterceptor
    {
        public bool Armed { get; set; }
        public int Failures { get; private set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Armed) { Armed = false; Failures++; throw new TimeoutException("P19 geçici commit hatası"); }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class AfterRead(string table) : DbCommandInterceptor
    {
        public Func<Task>? Callback { get; set; }
        public bool Ran { get; private set; }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Callback is { } callback && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)
                && command.CommandText.Contains(table, StringComparison.Ordinal))
            {
                Callback = null; Ran = true; await callback();
            }
            return result;
        }
    }
    private sealed class BeforeMutation : DbCommandInterceptor
    {
        public Func<Task>? Callback { get; set; }
        public bool Ran { get; private set; }
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Callback is { } callback && command.CommandText.StartsWith("DELETE", StringComparison.Ordinal)
                && command.CommandText.Contains("parent_notifications", StringComparison.Ordinal))
            { Callback = null; Ran = true; await callback(); }
            return result;
        }
    }
    private sealed class TargetDirectory(Guid parent, Guid student) : IParentNotificationDirectory
    {
        private ParentStudentNotificationTarget Target => new(student, new(parent, MembershipTier.Premium, new(true, true, true, true, true)));
        public Task<ParentNotificationTargetPage> ListApprovedTargetPageAsync(Guid? afterLinkId, int take, CancellationToken cancellationToken)
            => Task.FromResult(new ParentNotificationTargetPage([Target], null));
        public Task<IReadOnlyCollection<ParentStudentNotificationTarget>> ListAllApprovedTargetsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ParentStudentNotificationTarget>>([Target]);
        public Task<IReadOnlyCollection<ParentNotificationTarget>> GetApprovedParentsForStudentAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ParentNotificationTarget>>([Target.Target]);
    }
}
