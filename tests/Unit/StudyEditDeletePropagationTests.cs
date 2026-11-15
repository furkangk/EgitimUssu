using System.Text.Json;
using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Unit;

public sealed class StudyEditDeletePropagationTests
{
    public static readonly DateTime Now = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);
    public sealed class Clock : IClock { public DateTime UtcNow => Now; }
    public static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, Clock>();
        services.AddSingleton<ILocalCalendar>(new LocalCalendar("Europe/Istanbul", new Clock()));
        services.AddSingleton<IIdGenerator, FakeIdGen>();
        services.AddSingleton<IDomainEventMapper, JsonDomainEventMapper>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Postgres"] = $"InMemory:{Guid.NewGuid()}" }).Build();
        services.AddStudyModule(config);
        services.AddProgressTrackingModule(config);
        services.AddReportingModule(config);
        return services.BuildServiceProvider();
    }
    public static TestResult Test(Guid student, DateTime at, int correct = 16, string subject = "Mat", string? topic = "Türev")
        => new(Guid.NewGuid(), student, subject, topic, null, TestType.Subject, 20, correct, 0, 20-correct, 4, null, at, false, false, Now);
    public static StudySession Session(Guid student, int minutes = 60)
        => StudySession.CreateManual(Guid.NewGuid(), student, "Mat", "Türev", minutes, Now, null, false, false, Now);
    public static IntegrationEvent Event(Guid student, string name = "TestResultEditedDomainEvent")
        => new(Guid.NewGuid(), Now, name, "Study", JsonSerializer.Serialize(new { studentId = student }, IntegrationEventSerialization.Options));

    [Fact]
    public void Editing_and_deleting_raise_four_distinct_new_events()
    {
        var student = Guid.NewGuid();
        var session = Session(student); session.ClearDomainEvents();
        session.EditCompleted("Fizik", null, 20, null, Now);
        Assert.Single(session.DomainEvents.OfType<StudySessionEditedDomainEvent>());
        session.ClearDomainEvents(); session.NotifyDeleted();
        Assert.Single(session.DomainEvents.OfType<StudySessionDeletedDomainEvent>());
        var test = Test(student, Now); test.ClearDomainEvents();
        test.Edit("Fizik", null, null, TestType.Subject, 20, 10, 0, 10, 4, null, Now, Now);
        Assert.Single(test.DomainEvents.OfType<TestResultEditedDomainEvent>());
        test.ClearDomainEvents(); test.NotifyDeleted();
        Assert.Single(test.DomainEvents.OfType<TestResultDeletedDomainEvent>());
    }

    [Fact]
    public async Task Reader_uses_persisted_completed_records_and_excludes_deleted_and_other_students()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
        var student = Guid.NewGuid(); var deleted = Test(student, Now);
        db.TestResults.AddRange(deleted, Test(student, Now.AddDays(-1)), Test(Guid.NewGuid(), Now));
        db.StudySessions.AddRange(Session(student), Session(Guid.NewGuid()),
            StudySession.StartStopwatch(Guid.NewGuid(), student, "Mat", null, false, false, Now));
        await db.SaveChangesAsync(); db.TestResults.Remove(deleted); await db.SaveChangesAsync();
        var data = await scope.ServiceProvider.GetRequiredService<IStudyStatisticsReader>().ReadAsync(student, default);
        Assert.Single(data.Tests); Assert.Equal(60, Assert.Single(data.Sessions).EffectiveMinutes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MockExam_total_follows_edit_or_delete_in_the_same_repository_save(bool delete)
    {
        using var services = Services(); using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
        var student = Guid.NewGuid(); var test = Test(student, Now);
        var second = Test(student, Now, 10); var exam = new MockExam(Guid.NewGuid(), student, "TYT", Now, Now);
        exam.AddSubject(test); exam.AddSubject(second); db.MockExams.Add(exam); db.TestResults.AddRange(test, second);
        await db.SaveChangesAsync();
        if (delete) await new DeleteTestResultCommandHandler(scope.ServiceProvider.GetRequiredService<IStudyRepository>())
                .Handle(new(test.Id), default);
        else await new EditTestResultCommandHandler(scope.ServiceProvider.GetRequiredService<IStudyRepository>(), new Clock())
                .Handle(new(test.Id, "Mat", "Türev", "Subject", null, 20, 4, 0, 16, 4, null, Now), default);
        db.ChangeTracker.Clear();
        Assert.Equal(delete ? 10m : 14m, (await db.MockExams.SingleAsync()).TotalNet);
        Assert.Single(await db.OutboxMessages.Where(x => x.Type == (delete ? "TestResultDeletedDomainEvent" : "TestResultEditedDomainEvent")).ToArrayAsync());
    }

    [Fact]
    public void Mastery_rebuild_sorts_test_dates_and_preserves_lesson_contribution()
    {
        var student = Guid.NewGuid(); var mastery = new TopicMastery(Guid.NewGuid(), student, "Mat", "Türev", Now);
        mastery.RegisterLessonAttendance(60, Now);
        mastery.RebuildStudy(30, new[]
        {
            (Guid.NewGuid(), Now, 20, 18m), (Guid.NewGuid(), Now.AddDays(-2), 20, 4m),
            (Guid.NewGuid(), Now.AddDays(-1), 20, 10m)
        }, Now);
        Assert.Equal(3, mastery.TestAttemptCount); Assert.Equal(30, mastery.TotalStudyMinutes);
        Assert.Equal(ProgressTrend.Improving, mastery.Trend);
        Assert.Equal(.9m, mastery.RecentNetRatio); Assert.Equal(.5m, mastery.PriorNetRatio);
        Assert.Equal(60, mastery.TotalLessonMinutes); Assert.Equal(1, mastery.AttendedLessonCount);
        mastery.RebuildStudy(0, [], Now);
        Assert.Equal(0, mastery.TestAttemptCount); Assert.Null(mastery.AverageNetRatio);
        Assert.Null(mastery.PriorNetRatio); Assert.Null(mastery.RecentNetRatio);
        Assert.Equal(ProgressTrend.Stable, mastery.Trend); Assert.Equal(MasterySource.LessonOnly, mastery.Source);
        Assert.Equal(10m, mastery.MasteryScore);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Consumers_reconcile_changed_topic_date_and_deletion_even_with_late_record_event(bool deleted)
    {
        using var services = Services(); var student = Guid.NewGuid();
        Guid testId; Guid sessionId;
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
            var test = Test(student, Now.AddDays(-40)); var session = Session(student);
            db.AddRange(test, session); await db.SaveChangesAsync(); testId = test.Id; sessionId = session.Id;
        }
        await Reconcile(services, student);
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
            var test = await db.TestResults.FindAsync(testId); var session = await db.StudySessions.FindAsync(sessionId);
            if (deleted) { db.Remove(test!); db.Remove(session!); }
            else
            {
                test!.Edit("Fizik", "Optik", null, TestType.Subject, 20, 4, 0, 16, 4, null, Now, Now);
                session!.EditCompleted("Fizik", "Optik", 20, null, Now);
            }
            await db.SaveChangesAsync();
        }
        await Reconcile(services, student);
        // Farklı event id ile eski recorded bile gelse kaynak eski veriyi diriltmez.
        await Reconcile(services, student, "TestResultRecordedDomainEvent");
        using var read = services.CreateScope();
        var progress = read.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
        var reporting = read.ServiceProvider.GetRequiredService<ReportingDbContext>();
        var masteries = await progress.TopicMasteries.ToArrayAsync();
        var old = Assert.Single(masteries, x => x.Subject == "Mat");
        Assert.Equal(0, old.TestAttemptCount); Assert.Equal(0, old.TotalStudyMinutes);
        var weeks = await reporting.StudentStudyProjections.Include(x => x.Items).ToArrayAsync();
        Assert.Equal(deleted ? 0 : 20, weeks.Sum(x => x.StudyMinutes));
        Assert.Equal(deleted ? 0 : 1, weeks.Sum(x => x.TestCount));
        var months = await reporting.StudentPerformanceProjections.Include(x => x.Items).ToArrayAsync();
        Assert.Equal(deleted ? 0 : 1, months.Sum(x => x.TestCount));
        Assert.All(months.Where(x => x.Subject == "Mat"), x => Assert.Equal(0, x.TestCount));
        if (!deleted)
        {
            Assert.Equal(.2m, Assert.Single(months, x => x.Subject == "Fizik").AverageNet);
            Assert.Equal(1, Assert.Single(masteries, x => x.Subject == "Fizik").TestAttemptCount);
        }
    }

    private static async Task Reconcile(ServiceProvider services, Guid student, string name = "TestResultEditedDomainEvent")
    {
        using var scope = services.CreateScope(); var sp = scope.ServiceProvider;
        var source = sp.GetRequiredService<IStudyStatisticsReader>(); var clock = sp.GetRequiredService<IClock>();
        var message = Event(student, name);
        var progress = new StudyStatisticsProgressHandler(sp.GetRequiredService<ProgressTrackingDbContext>(),
            sp.GetRequiredService<MasteryService>(), source, clock);
        var reporting = new StudyStatisticsProjectionHandler(sp.GetRequiredService<ReportingDbContext>(), source, clock,
            sp.GetRequiredService<ILocalCalendar>());
        await progress.HandleAsync(message); await progress.HandleAsync(message);
        await reporting.HandleAsync(message); await reporting.HandleAsync(message);
    }
}
