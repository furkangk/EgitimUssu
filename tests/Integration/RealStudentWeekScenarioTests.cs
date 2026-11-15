using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Modules.Parents.Infrastructure;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Modules.LessonSessions.Infrastructure;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Modules.Settings.Domain;
using EgitimUssu.Modules.Settings.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Storage;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Kernel.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealStudentWeekScenarioTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Reporting_merge_unions_items_preserves_canonical_conflicts_and_source_reconciliation()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infra = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var from = Guid.NewGuid(); var to = Guid.NewGuid(); var teacher = Guid.NewGuid();
        var session1 = Guid.NewGuid(); var session2 = Guid.NewGuid(); var test = Guid.NewGuid();
        var report = Guid.NewGuid(); var now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
        var message = Merge(from, to, now);
        try
        {
            await using (var seed = factory.Services.CreateAsyncScope())
            {
                var db = seed.ServiceProvider.GetRequiredService<ReportingDbContext>();
                var dashboard = new TeacherDashboardProjection(teacher);
                dashboard.TrackStudent(from); dashboard.TrackStudent(to); db.Add(dashboard);
                var monthly = new TeacherMonthlyProjection(teacher, 2026, 11);
                monthly.TrackCompletedLesson(Guid.NewGuid(), from); monthly.TrackCompletedLesson(Guid.NewGuid(), to); db.Add(monthly);
                var a = new StudentStudyProjection(from, 2026, 46);
                a.TrackSession(session1, 30); a.TrackSession(session2, 999); a.TrackTest(test);
                var b = new StudentStudyProjection(to, 2026, 46); b.TrackSession(session2, 60);
                var otherWeek = new StudentStudyProjection(from, 2026, 45); otherWeek.TrackSession(Guid.NewGuid(), 15);
                db.AddRange(a, b, otherWeek);
                var p = new StudentPerformanceProjection(from, "Mat", 2026, 11);
                p.TrackTopicMastery("Türev", 99); p.TrackTopicMastery("İntegral", 50); p.TrackTestNet(test, .8m);
                var q = new StudentPerformanceProjection(to, "Mat", 2026, 11); q.TrackTopicMastery("Türev", 70);
                var otherMonth = new StudentPerformanceProjection(from, "Fizik", 2026, 10); otherMonth.TrackTopicMastery("Optik", 20);
                db.AddRange(p, q, otherMonth);
                db.Add(new StudentReport(report, from, teacher, new(2026,11,1), new(2026,11,30), "kept/key", "report.pdf", "application/pdf", 20, now));
                await db.SaveChangesAsync();
                // Güncel kaynak sonraki Study olayında union'ı yeniden hesaplayacak; iki profilin verisi korunmalı.
                var study = seed.ServiceProvider.GetRequiredService<StudyDbContext>();
                study.Add(StudySession.CreateManual(session1, from, "Mat", "Türev", 30, now, null, true, true, now));
                study.Add(StudySession.CreateManual(session2, to, "Mat", "Türev", 60, now, null, true, true, now));
                study.Add(new TestResult(test, from, "Mat", "Türev", null, TestType.Subject, 20, 16, 0, 4, 4, null, now, true, true, now));
                await study.SaveChangesAsync();
                // Identity ölçümü: modelde profile bağlanan StudentId alanı bulunmuyor.
                Assert.DoesNotContain(seed.ServiceProvider.GetRequiredService<IdentityDbContext>().Model.GetEntityTypes()
                    .SelectMany(x => x.GetProperties()), x => x.Name == "StudentId");
            }
            await Publish(factory, message);
            await Publish(factory, message); // inbox replay
            await Publish(factory, message with { EventId = Guid.NewGuid() }); // business replay
            await using (var read = factory.Services.CreateAsyncScope())
            {
                var db = read.ServiceProvider.GetRequiredService<ReportingDbContext>();
                Assert.Empty(await db.StudentStudyProjections.Where(x => x.StudentId == from).ToArrayAsync());
                Assert.Empty(await db.StudentPerformanceProjections.Where(x => x.StudentId == from).ToArrayAsync());
                var weeks = await db.StudentStudyProjections.Include(x => x.Items).Where(x => x.StudentId == to).ToArrayAsync();
                Assert.Equal(2, weeks.Length);
                var week = Assert.Single(weeks, x => x.WeekOfYear == 46);
                Assert.Equal((90, 2, 1), (week.StudyMinutes, week.SessionCount, week.TestCount));
                var months = await db.StudentPerformanceProjections.Include(x => x.Items).Where(x => x.StudentId == to).ToArrayAsync();
                Assert.Equal(2, months.Length);
                var month = Assert.Single(months, x => x.Subject == "Mat");
                Assert.Equal((60, .8m, 1), (month.MasteryScore, month.AverageNet, month.TestCount));
                Assert.Equal(1, (await db.TeacherDashboardProjections.Include(x => x.Items).SingleAsync(x => x.TeacherUserId == teacher)).ActiveStudentCount);
                Assert.Equal(1, (await db.TeacherMonthlyProjections.Include(x => x.Items).SingleAsync(x => x.TeacherUserId == teacher)).ActiveStudentCount);
                var pdf = await db.StudentReports.SingleAsync(x => x.Id == report);
                Assert.Equal(to, pdf.StudentId); Assert.Equal("kept/key", pdf.StorageKey);
                Assert.Equal(1, await db.InboxMessages.CountAsync(x => x.Handler == nameof(ReportingStudentMergedHandler)
                    && x.EventId == message.EventId));
            }
            // Mevcut T5 tüketicisi tüm kaynağı okur; taşınan seans/test bir sonraki olayda kaybolmaz.
            await Publish(factory, Event("Study", "StudySessionEditedDomainEvent", new { studentId = to }, now));
            await using var check = factory.Services.CreateAsyncScope();
            var reportDb = check.ServiceProvider.GetRequiredService<ReportingDbContext>();
            var current = await reportDb.StudentStudyProjections.Include(x => x.Items).SingleAsync(x => x.StudentId == to && x.WeekOfYear == 46);
            Assert.Equal((90, 2, 1), (current.StudyMinutes, current.SessionCount, current.TestCount));
        }
        finally { await Cleanup(factory, from, to); }
    }

    [SkippableFact]
    public async Task Notification_merge_preserves_delivery_state_and_local_week_dedup()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infra = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.Replace(ServiceDescriptor.Singleton<IEntitlementDirectory, PremiumEntitlements>())));
        var from = Guid.NewGuid(); var to = Guid.NewGuid(); var parent = Guid.NewGuid();
        var now = new DateTime(2026, 11, 15, 21, 30, 0, DateTimeKind.Utc); // Pazartesi 00:30 TR
        var reminderId = Guid.NewGuid(); var canonicalNotice = Guid.NewGuid();
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            db.Add(new ParentNotification(Guid.NewGuid(), parent, from, ParentNotificationType.WeeklySummary, "Eski", "hazır", now));
            db.Add(new ParentNotification(canonicalNotice, parent, to, ParentNotificationType.WeeklySummary, "Kanonik", "hazır", now));
            db.Add(new ParentNotification(Guid.NewGuid(), parent, from, ParentNotificationType.WeeklySummary, "Önceki", "hazır", now.AddHours(-2)));
            db.Add(new ParentNotification(Guid.NewGuid(), parent, from, ParentNotificationType.NewAssignment, "Ödev", "yeni", now));
            var reminder = new LessonReminder(reminderId, Guid.NewGuid(), Guid.NewGuid(), from, "Ders", "hatırlatma", now.AddHours(1), now,
                NotificationChannel.Push, ReminderStatus.Pending, now);
            reminder.MarkFailed("temporary", now); db.Add(reminder);
            await db.SaveChangesAsync();
            var parents = seed.ServiceProvider.GetRequiredService<ParentsDbContext>();
            parents.Add(new ParentProfile(Guid.NewGuid(), parent, "Veli", null, null, now));
            var link = new ParentChildLink(Guid.NewGuid(), parent, to, "Çocuk", null, null, true, now);
            link.Approve(parent, null, now); parents.Add(link); await parents.SaveChangesAsync();
        }
        var message = Merge(from, to, now);
        await Publish(factory, message); await Publish(factory, message);
        await Publish(factory, message with { EventId = Guid.NewGuid() });
        await using var read = factory.Services.CreateAsyncScope();
        var notifications = read.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        Assert.Empty(await notifications.ParentNotifications.Where(x => x.StudentId == from).ToArrayAsync());
        var rows = await notifications.ParentNotifications.Where(x => x.StudentId == to).ToArrayAsync();
        Assert.Equal(3, rows.Length); Assert.Contains(rows, x => x.Id == canonicalNotice);
        var r = await notifications.LessonReminders.SingleAsync(x => x.Id == reminderId);
        Assert.Equal(to, r.StudentId); Assert.Equal(ReminderStatus.Pending, r.Status); Assert.Equal(1, r.AttemptCount);
        // Tekillik sadece bildirim satırında değil processor'ın gerçekten sorguladığı v2 marker'da olmalı.
        await read.ServiceProvider.GetRequiredService<IParentWeeklySummaryProcessor>().RunAsync(now);
        Assert.Equal(2, await notifications.ParentNotifications.CountAsync(x => x.StudentId == to && x.Type == ParentNotificationType.WeeklySummary));
        await read.ServiceProvider.GetRequiredService<IParentWeeklySummaryProcessor>().RunAsync(now.AddDays(7));
        Assert.Equal(3, await notifications.ParentNotifications.CountAsync(x => x.StudentId == to && x.Type == ParentNotificationType.WeeklySummary));
    }

    [SkippableFact]
    public async Task Student_week_http_outbox_parent_summary_and_payment_failure_recovery()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infra = RealInfrastructure.Use(fixture);
        var root = Path.Combine(Path.GetTempPath(), $"student-week-{Guid.NewGuid():N}");
        var fault = new PaymentFailure();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.Replace(ServiceDescriptor.Singleton<IEntitlementDirectory, PremiumEntitlements>());
            s.RemoveAll<IFileStorage>();
            s.AddSingleton<IFileStorage>(new LocalFileStorage(Options.Create(new StorageOptions { LocalRootPath = root })));
            s.AddDbContext<PaymentsDbContext>(o => o.AddInterceptors(fault));
        }));
        using var studentClient = factory.CreateClient(); var student = await Login(studentClient, 3, "10.198.1.1");
        using var teacherClient = factory.CreateClient(); var teacher = await Login(teacherClient, 2, "10.198.1.2");
        using var parentClient = factory.CreateClient(); var parent = await Login(parentClient, 4, "10.198.1.3");
        var assignment = Guid.NewGuid(); var lesson = Guid.NewGuid();
        try
        {
            var clock = factory.Services.GetRequiredService<IClock>();
            var calendar = factory.Services.GetRequiredService<ILocalCalendar>();
            var now = clock.UtcNow; var day = calendar.StudyDay(now);
            await using (var seed = factory.Services.CreateAsyncScope())
            {
                var students = seed.ServiceProvider.GetRequiredService<StudentsDbContext>();
                // #267 P21 borcu: ödev öğrenci yetkisi henüz profilId yerine UserId karşılaştırıyor.
                students.Add(new StudentProfile(student, student, null, null, "Hafta Öğrencisi", "7", null, null, null, null,
                    StudentOrigin.SelfRegistered, true, now));
                var teacherLink = new TeacherStudentLink(Guid.NewGuid(), teacher, student, TeacherStudentLinkStatus.Linked, now);
                teacherLink.SetRate(150, "TRY", now); students.Add(teacherLink); await students.SaveChangesAsync();
                var parents = seed.ServiceProvider.GetRequiredService<ParentsDbContext>();
                parents.Add(new ParentProfile(Guid.NewGuid(), parent, "Hafta Velisi", null, null, now));
                var parentLink = new ParentChildLink(Guid.NewGuid(), parent, student, "Öğrenci", null, null, true, now);
                parentLink.Approve(parent, student, now); parents.Add(parentLink); await parents.SaveChangesAsync();
                var settings = seed.ServiceProvider.GetRequiredService<SettingsDbContext>();
                settings.Add(new UserSetting(Guid.NewGuid(), student, true, true, true, true, true, true, true, true,
                    PrivacyLevel.Standard, SessionTerminationPolicy.KeepLatest, now)); await settings.SaveChangesAsync();
                var assignments = seed.ServiceProvider.GetRequiredService<AssignmentsDbContext>();
                assignments.Add(new Assignment(assignment, student, teacher, null, "Hafta ödevi", null, now.AddDays(1),
                    AssignmentStatus.Pending, null, now, null)); await assignments.SaveChangesAsync();
            }
            await Drain(factory);
            await Json(await studentClient.PutAsJsonAsync($"/api/study/students/{student}/goals", new { dailyGoalMinutes = 60, streakThresholdPercent = 100 }));
            foreach (var at in new[] { calendar.ToUtc(day.AddDays(-2), new TimeOnly(12, 0)),
                calendar.ToUtc(day, new TimeOnly(1, 30)), now.AddMinutes(-1) })
                await Json(await studentClient.PostAsJsonAsync("/api/study/sessions/manual", new
                { studentId = student, subject = "Mat", topic = "Türev", effectiveMinutes = 60, studiedOnUtc = at }));
            var streak = await Json(await studentClient.GetAsync($"/api/study/students/{student}/streak"));
            Assert.Equal(3, streak.GetProperty("currentStreakDays").GetInt32());
            await Drain(factory);
            var before = await Statistics(factory, student);
            Assert.Equal((180, 3, 0), before);
            var result = await Json(await studentClient.PostAsJsonAsync("/api/study/test-results", new
            { studentId = student, subject = "Mat", topic = "Türev", testType = "Subject", totalQuestions = 20,
                correct = 16, wrong = 0, blank = 4, takenOnUtc = now.AddMinutes(-1) }));
            await Drain(factory); Assert.Equal((180, 3, 1), await Statistics(factory, student));
            Assert.Equal(HttpStatusCode.OK, (await studentClient.DeleteAsync($"/api/study/test-results/{result.GetProperty("id").GetGuid()}")).StatusCode);
            await Drain(factory); Assert.Equal(before, await Statistics(factory, student));
            await Json(await Upload(studentClient, assignment, "ilk sürüm"));
            await Drain(factory); await AssertAssignments(parentClient, parent, student, 0, 1);
            await Json(await teacherClient.PostAsJsonAsync($"/api/assignments/{assignment}/return", new { feedback = "ikinci soru" }));
            await Drain(factory); await AssertAssignments(parentClient, parent, student, 1, 0);
            await Json(await Upload(studentClient, assignment, "ikinci sürüm"));
            await Drain(factory); await AssertAssignments(parentClient, parent, student, 0, 1);
            await Json(await teacherClient.PostAsJsonAsync($"/api/assignments/{assignment}/approve", new { feedback = "tamam" }));
            var graded = await Json(await teacherClient.PostAsJsonAsync($"/api/assignments/{assignment}/grade", new { score = 92, feedback = "iyi" }));
            Assert.Equal(92, graded.GetProperty("score").GetInt32());
            Assert.Equal(2, graded.GetProperty("submissionVersions").GetArrayLength());
            Assert.Equal("ikinci soru", graded.GetProperty("submissionVersions")[0].GetProperty("teacherFeedback").GetString());
            Assert.Equal("ilk sürüm", await (await studentClient.GetAsync($"/api/assignments/{assignment}/attachment?version=1")).Content.ReadAsStringAsync());
            await Drain(factory);
            var panel = await Json(await parentClient.GetAsync($"/api/parents/{parent}/children/{student}/dashboard"));
            Assert.Equal(180, panel.GetProperty("study").GetProperty("weeklyStudyMinutes").GetInt32());
            Assert.Equal(3, panel.GetProperty("study").GetProperty("streakDays").GetInt32());
            Assert.Equal(1, panel.GetProperty("assignments").GetProperty("completedCount").GetInt32());
            await using (var summary = factory.Services.CreateAsyncScope())
            {
                await summary.ServiceProvider.GetRequiredService<IParentWeeklySummaryProcessor>().RunAsync(now);
                await summary.ServiceProvider.GetRequiredService<IParentWeeklySummaryProcessor>().RunAsync(now);
                Assert.Equal(1, await summary.ServiceProvider.GetRequiredService<NotificationsDbContext>().ParentNotifications
                    .CountAsync(x => x.ParentUserId == parent && x.StudentId == student && x.Type == ParentNotificationType.WeeklySummary));
            }
            // Gerçek Payments handler'ının SaveChanges'i hata verir; Reporting aynı outbox turunda ilerler.
            fault.Lesson = lesson;
            await using (var complete = factory.Services.CreateAsyncScope())
            {
                var sessions = complete.ServiceProvider.GetRequiredService<LessonSessionsDbContext>();
                var row = new LessonSession(lesson, null, teacher, student, "Mat", now.AddHours(-2), null, null, null,
                    StudentAttendanceStatus.Unknown, LessonSessionStatus.Planned, "Türev", null, null, now, null);
                Assert.True(row.Complete(now.AddHours(-2), now.AddHours(-1), StudentAttendanceStatus.Attended, "Türev", null, null, true, now).IsSuccess);
                sessions.Add(row); await sessions.SaveChangesAsync();
            }
            await Drain(factory);
            Guid failedId;
            await using (var check = factory.Services.CreateAsyncScope())
            {
                var sessions = check.ServiceProvider.GetRequiredService<LessonSessionsDbContext>();
                var failed = await sessions.OutboxMessages.SingleAsync(x => x.Type == nameof(LessonSessionCompletedDomainEvent) && x.Payload.Contains(lesson.ToString()));
                Assert.Null(failed.ProcessedOnUtc); Assert.Equal(1, failed.RetryCount);
                Assert.Contains(nameof(LessonSessionCompletedPaymentHandler), failed.Error);
                failedId = failed.Id;
                var reporting = check.ServiceProvider.GetRequiredService<ReportingDbContext>();
                Assert.Equal(1, await reporting.Set<TeacherMonthlyItem>().CountAsync(x => x.ItemId == lesson && x.Kind == TeacherMonthlyItemKind.CompletedLesson));
                Assert.False(await check.ServiceProvider.GetRequiredService<PaymentsDbContext>().PaymentRecords.AnyAsync(x => x.RelatedLessonSessionId == lesson));
                failed.NextAttemptUtc = now.AddSeconds(-1); await sessions.SaveChangesAsync();
            }
            fault.Enabled = false;
            await Drain(factory);
            await using (var recovered = factory.Services.CreateAsyncScope())
            {
                Assert.NotNull((await recovered.ServiceProvider.GetRequiredService<LessonSessionsDbContext>().OutboxMessages.SingleAsync(x => x.Id == failedId)).ProcessedOnUtc);
                Assert.Equal(1, await recovered.ServiceProvider.GetRequiredService<PaymentsDbContext>().PaymentRecords.CountAsync(x => x.RelatedLessonSessionId == lesson));
                Assert.Equal(1, await recovered.ServiceProvider.GetRequiredService<ReportingDbContext>().Set<TeacherMonthlyItem>().CountAsync(x => x.ItemId == lesson && x.Kind == TeacherMonthlyItemKind.CompletedLesson));
            }
        }
        finally
        {
            await Cleanup(factory, student);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static async Task AssertAssignments(HttpClient parentClient, Guid parent, Guid student, int open, int completed)
    {
        var panel = await Json(await parentClient.GetAsync($"/api/parents/{parent}/children/{student}/dashboard"));
        Assert.Equal(open, panel.GetProperty("assignments").GetProperty("openCount").GetInt32());
        Assert.Equal(completed, panel.GetProperty("assignments").GetProperty("completedCount").GetInt32());
        Assert.Equal(open + completed, panel.GetProperty("assignments").GetProperty("totalCount").GetInt32());
    }

    private sealed class PaymentFailure : SaveChangesInterceptor
    {
        public Guid Lesson { get; set; }
        public bool Enabled { get; set; } = true;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && Lesson != Guid.Empty && eventData.Context!.ChangeTracker.Entries<PaymentRecord>()
                .Any(x => x.State == EntityState.Added && x.Entity.RelatedLessonSessionId == Lesson))
                throw new InvalidOperationException("P19 injected payment failure");
            return ValueTask.FromResult(result);
        }
    }
    private static async Task<(int Minutes, int Sessions, int Tests)> Statistics(WebApplicationFactory<Program> factory, Guid student)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var weeks = await scope.ServiceProvider.GetRequiredService<ReportingDbContext>().StudentStudyProjections
            .Include(x => x.Items).Where(x => x.StudentId == student).ToArrayAsync();
        var mastery = await scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>().TopicMasteries.Where(x => x.StudentId == student).ToArrayAsync();
        var tests = weeks.Sum(x => x.TestCount);
        Assert.Equal(tests, mastery.Sum(x => x.TestAttemptCount));
        Assert.Equal(weeks.Sum(x => x.StudyMinutes), mastery.Sum(x => x.TotalStudyMinutes));
        return (weeks.Sum(x => x.StudyMinutes), weeks.Sum(x => x.SessionCount), tests);
    }
    private static async Task Drain(WebApplicationFactory<Program> factory)
    {
        for (var i = 0; i < 30; i++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().DispatchPendingAsync() == 0) return;
        }
        Assert.Fail("Outbox 30 turda boşalmadı.");
    }
    private static async Task<Guid> Login(HttpClient client, int role, string ip)
    {
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        var body = await Json(await client.PostAsJsonAsync("/api/identity/register", new
        { email = $"week-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Hafta", lastName = "Test", roles = new[] { role } }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return body.GetProperty("userId").GetGuid();
    }
    private static async Task<HttpResponseMessage> Upload(HttpClient client, Guid id, string text)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(text)) { Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") } }, "file", "odev.pdf");
        return await client.PostAsync($"/api/assignments/{id}/submission", form);
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
            using var doc = JsonDocument.Parse(body); return doc.RootElement.Clone();
        }
    }

    private static IntegrationEvent Merge(Guid from, Guid to, DateTime now)
        => Event("Students", "StudentProfilesMergedDomainEvent", new StudentProfilesMergedIntegrationEvent(from, to), now);
    private static IntegrationEvent Event(string module, string name, object payload, DateTime now)
        => new(Guid.NewGuid(), now, name, module, JsonSerializer.Serialize(payload, IntegrationEventSerialization.Options));
    private static async Task Publish(WebApplicationFactory<Program> factory, IntegrationEvent message)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(message);
    }
    private static async Task Cleanup(WebApplicationFactory<Program> factory, params Guid[] students)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var study = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
        var progress = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
        foreach (var student in students)
        {
            await study.OutboxMessages.Where(x => x.Payload.Contains(student.ToString())).ExecuteDeleteAsync();
            await progress.OutboxMessages.Where(x => x.Payload.Contains(student.ToString())).ExecuteDeleteAsync();
        }
        await progress.TopicMasteries.Where(x => students.Contains(x.StudentId)).ExecuteDeleteAsync();
        await progress.ProgressSnapshots.Where(x => students.Contains(x.StudentId)).ExecuteDeleteAsync();
    }
    private sealed class PremiumEntitlements : IEntitlementDirectory
    {
        private static readonly Entitlement Premium = new(MembershipTier.Premium, null, int.MaxValue, false, true, true, true);
        public Task<Entitlement> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult(Premium);
        public Task<IReadOnlyDictionary<Guid, Entitlement>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<Guid, Entitlement>>(userIds.Distinct().ToDictionary(x => x, _ => Premium));
    }
}
