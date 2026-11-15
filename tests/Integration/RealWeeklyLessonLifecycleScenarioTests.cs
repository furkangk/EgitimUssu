using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Modules.LessonSessions.Infrastructure;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Modules.Teachers.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealWeeklyLessonLifecycleScenarioTests(ContainerFixture fixture)
{
    private sealed class BusinessClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc);
    }

    [SkippableFact]
    public async Task Three_week_lifecycle_keeps_sessions_fees_reminders_dashboard_and_monthly_report_consistent()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infrastructure = RealInfrastructure.Use(fixture);
        var clock = new BusinessClock();
        // Sistem IClock değişmez: JWT ve outbox gerçek zamanda; yalnız iş zamanı ilerler.
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                for (var i = 0; i < services.Count; i++)
                {
                    var type = services[i].ImplementationType;
                    if (type == typeof(LessonSessionCompletedPaymentHandler) || type == typeof(LessonCancelledPaymentHandler))
                        services[i] = ServiceDescriptor.Scoped<IIntegrationEventHandler>(
                            sp => (IIntegrationEventHandler)ActivatorUtilities.CreateInstance(sp, type, clock));
                }
                services.RemoveAll<IHostedService>(); // Zamanı ilerletirken otomatik tarayıcılarla yarışma.
                services.AddSingleton<ILocalCalendar>(new LocalCalendar("Europe/Istanbul", clock));
                services.Replace(ServiceDescriptor.Scoped<ICommandHandler<CancelLessonScheduleCommand, Result<LessonScheduleResponse>>>(
                    sp => ActivatorUtilities.CreateInstance<CancelLessonScheduleCommandHandler>(sp, clock)));
                services.Replace(ServiceDescriptor.Scoped<ICommandHandler<CompleteLessonSessionCommand, Result<LessonSessionResponse>>>(
                    sp => ActivatorUtilities.CreateInstance<CompleteLessonSessionCommandHandler>(sp, clock)));
                services.Replace(ServiceDescriptor.Scoped<LessonReminderPlanningProcessor>(
                    sp => ActivatorUtilities.CreateInstance<LessonReminderPlanningProcessor>(sp, clock)));
            }));
        using var teacher = Client(factory, "10.195.1.1");
        using var student = Client(factory, "10.195.1.2");
        using var parent = Client(factory, "10.195.1.3");
        var teacherId = await Register(teacher, 2);
        var studentUserId = await Register(student, 3);
        await Register(parent, 4);
        await Json(await teacher.PostAsJsonAsync("/api/teachers/profiles", new
        {
            userId = teacherId, fullName = "Yaşam Döngüsü Öğretmeni", subject = "Matematik", city = "İstanbul", district = "Kadıköy",
            lessonFormat = 2, experienceYears = 1, educationLevel = "Lisans", hourlyRateAmount = 100, currency = "TRY",
            availabilitySlots = Array.Empty<object>(), subjects = Array.Empty<string>(), certificates = Array.Empty<object>()
        }));
        await Json(await teacher.PutAsJsonAsync($"/api/teachers/profiles/{teacherId}/cancellation-policy", new { noticeHours = 24, chargePercent = 100 }));
        var studentId = (await Json(await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            createdByTeacherUserId = teacherId, fullName = "Yaşam Döngüsü Öğrencisi", gradeLevel = "8", origin = 1, subjects = Array.Empty<object>()
        }))).GetProperty("id").GetGuid();
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherId, studentId, studentUserId);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PutAsJsonAsync($"/api/students/teachers/{teacherId}/students/{studentId}/rate",
            new { agreedRateAmount = 100, currency = "TRY" })).StatusCode);
        var invite = await Json(await teacher.PostAsJsonAsync($"/api/students/profiles/{studentId}/parent-invite", new { teacherUserId = teacherId }));
        var parentLink = await Json(await parent.PostAsJsonAsync("/api/parents/children/claim-invite", new { inviteCode = invite.GetProperty("inviteCode").GetString() }));
        await Drain(factory);
        // Bağlı öğrenci onaylar; veli görünürlüğü gerçek HTTP yetki yolundan sınanır.
        if (parentLink.GetProperty("status").GetString() == "Pending")
            await Json(await student.PostAsync($"/api/parents/children/{parentLink.GetProperty("id").GetGuid()}/approve", null));
        var anchor = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc); // 15:00 TR
        var second = anchor.AddDays(7);
        var third = anchor.AddDays(14);
        var moved = third.AddHours(3); // 18:00 TR
        var series = (await Json(await teacher.PostAsJsonAsync("/api/scheduling/lessons", new
        {
            teacherUserId = teacherId, studentId, subject = "Matematik", lessonFormat = 2,
            startAtUtc = anchor, endAtUtc = anchor.AddHours(1), timeZone = "Europe/Istanbul",
            recurrenceRule = "FREQ=WEEKLY;COUNT=3", reminderOffsetMinutes = 15
        }))).GetProperty("id").GetGuid();
        try
        {
            var firstId = await Complete(teacher, series, anchor, anchor);
            await Drain(factory);
            Assert.Single(await Payments(factory, studentId), p => p.ItemType == BillingItemType.LessonFee);
            Assert.Equal(1, (await Json(await teacher.GetAsync($"/api/reporting/teachers/{teacherId}/dashboard"))).GetProperty("todayLessonCount").GetInt32());

            clock.UtcNow = second.AddHours(-3);
            await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{series}/cancel", new
                { reason = 2, isChargeable = true, scope = 1, occurrenceStartAtUtc = second }));
            await Drain(factory);
            var fee = Assert.Single(await Payments(factory, studentId), p => p.ItemType == BillingItemType.LateCancellation);
            Assert.Equal(100m, fee.ExpectedAmount);
            Assert.Equal(second, fee.OccurrenceStartUtc);
            Assert.Equal(0, (await Json(await teacher.GetAsync($"/api/reporting/teachers/{teacherId}/dashboard"))).GetProperty("todayLessonCount").GetInt32());

            await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{series}/reschedule", new
                { newStartAtUtc = moved, newEndAtUtc = moved.AddHours(1), scope = 1, occurrenceStartAtUtc = third }));
            clock.UtcNow = third.AddHours(-1);
            await Drain(factory);
            await using (var scope = factory.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<LessonReminderPlanningProcessor>().RunAsync();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var reminders = await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().LessonReminders.AsNoTracking()
                    .Where(r => r.LessonScheduleId == series && r.Status == ReminderStatus.Pending).ToArrayAsync();
                Assert.NotEmpty(reminders);
                Assert.All(reminders, r =>
                {
                    Assert.Equal(third, r.OccurrenceStartUtc);
                    Assert.Equal(moved.AddMinutes(-15), r.RemindAtUtc); // 17:45 TR
                    Assert.Contains("18:00", r.Message);
                });
                Assert.Contains(reminders, r => r.RecipientUserId == teacherId);
                Assert.Contains(reminders, r => r.RecipientUserId == studentUserId);
            }
            clock.UtcNow = moved.AddHours(1);
            var thirdId = await Complete(teacher, series, third, moved);
            Assert.NotEqual(firstId, thirdId);
            await Drain(factory);
            // İkinci tamamlama yeni borç/katılım üretmez.
            Assert.Equal(thirdId, await Complete(teacher, series, third, moved));
            await Drain(factory);
            var payments = await Payments(factory, studentId);
            Assert.Equal(2, payments.Count(p => p.ItemType == BillingItemType.LessonFee));
            Assert.Equal(3, payments.Length);
            Assert.Equal(300m, payments.Sum(p => p.ExpectedAmount));
            foreach (var reader in new[] { student, parent })
            {
                var sessions = (await Json(await reader.GetAsync($"/api/lesson-sessions?studentId={studentId}"))).EnumerateArray().ToArray();
                Assert.Equal(new[] { firstId, thirdId }.Order(), sessions.Where(s => s.GetProperty("status").GetString() == "Completed")
                    .Select(s => s.GetProperty("id").GetGuid()).Order());
            }
            Assert.Equal(1, (await Json(await teacher.GetAsync($"/api/reporting/teachers/{teacherId}/dashboard"))).GetProperty("todayLessonCount").GetInt32());
            await using (var replay = factory.Services.CreateAsyncScope())
            {
                var db = replay.ServiceProvider.GetRequiredService<SchedulingDbContext>();
                var message = await db.OutboxMessages.SingleAsync(m => m.Type == "LessonCancellationAppliedDomainEvent" && m.Payload.Contains(series.ToString()));
                var original = JsonSerializer.Deserialize<IntegrationEvent>(message.Payload, IntegrationEventSerialization.Options)!;
                for (var n = 0; n < 2; n++)
                {
                    var envelope = original with { EventId = Guid.NewGuid() };
                    var handler = replay.ServiceProvider.GetServices<IIntegrationEventHandler>().OfType<TeacherMonthlyOccurrenceCancelledProjectionHandler>().Single();
                    Assert.True(handler.CanHandle(envelope));
                    await handler.HandleAsync(envelope);
                }
            }
            var monthly = await Json(await teacher.GetAsync($"/api/reporting/teachers/{teacherId}/monthly?year=2026&month=11"));
            Assert.Equal(2, monthly.GetProperty("completedLessonCount").GetInt32());
            Assert.Equal(1, monthly.GetProperty("cancelledLessonCount").GetInt32());
            Assert.Equal(300m, monthly.GetProperty("expectedAmount").GetDecimal());
            await using var check = factory.Services.CreateAsyncScope();
            Assert.Equal(2, await check.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>().TopicMasteries
                .Where(t => t.StudentId == studentId).SumAsync(t => t.AttendedLessonCount));
        }
        finally
        {
            await using var cleanup = factory.Services.CreateAsyncScope();
            await cleanup.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>().TopicMasteries.Where(t => t.StudentId == studentId).ExecuteDeleteAsync();
            await cleanup.ServiceProvider.GetRequiredService<TeachersDbContext>().TeacherProfiles.Where(t => t.UserId == teacherId).ExecuteDeleteAsync();
            await parent.PostAsync($"/api/parents/children/{parentLink.GetProperty("id").GetGuid()}/revoke", null);
            await Drain(factory);
        }
    }

    [SkippableFact]
    public async Task Monthly_migration_preserves_legacy_items_and_downgrades_multiple_occurrences()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        var database = $"monthly_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(fixture.PostgresConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = database }.ConnectionString;
            await using var db = new ReportingDbContext(new DbContextOptionsBuilder<ReportingDbContext>().UseNpgsql(connection).Options, new NoOpDomainEventMapper());
            var migrator = db.GetService<IMigrator>();
            var previous = db.Database.GetMigrations().ToArray()[^2];
            await migrator.MigrateAsync(previous);
            var teacher = Guid.NewGuid(); var series = Guid.NewGuid(); var payment = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO reporting.teacher_monthly_projections ("TeacherUserId", "Year", "Month", "CollectedAmount", "LastAppliedEventId", "UpdatedOnUtc")
                VALUES ({teacher}, 2026, 11, 0, {Guid.Empty}, {DateTime.UtcNow});
                INSERT INTO reporting.teacher_monthly_items ("TeacherUserId", "Year", "Month", "Kind", "ItemId", "Amount")
                VALUES ({teacher}, 2026, 11, 2, {series}, NULL), ({teacher}, 2026, 11, 3, {payment}, 100)
                """);
            await migrator.MigrateAsync();
            var projection = await db.TeacherMonthlyProjections.Include(p => p.Items).SingleAsync();
            Assert.All(projection.Items, item => Assert.Equal(DateTime.UnixEpoch, item.OccurrenceStartUtc));
            projection.TrackCancelledOccurrence(series, new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc));
            projection.TrackCancelledOccurrence(series, new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc));
            await db.SaveChangesAsync();
            Assert.Equal(3, projection.CancelledLessonCount);
            await migrator.MigrateAsync(previous);
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            var restored = await db.TeacherMonthlyProjections.Include(p => p.Items).SingleAsync();
            Assert.Equal(1, restored.CancelledLessonCount);
            Assert.Equal(100m, restored.ExpectedAmount);
            Assert.Equal(2, restored.Items.Count);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Guid> Complete(HttpClient teacher, Guid schedule, DateTime key, DateTime visible)
    {
        var session = await Json(await teacher.PostAsJsonAsync("/api/lesson-sessions/from-schedule", new { lessonScheduleId = schedule, occurrenceStartAtUtc = key }));
        var id = session.GetProperty("id").GetGuid();
        Assert.Equal(key, session.GetProperty("occurrenceStartAtUtc").GetDateTime());
        Assert.Equal(visible, session.GetProperty("plannedStartAtUtc").GetDateTime());
        await Json(await teacher.PostAsJsonAsync($"/api/lesson-sessions/{id}/complete", new
        {
            actualStartAtUtc = visible, actualEndAtUtc = visible.AddHours(1), attendanceStatus = 2,
            topicTitle = "Kesirler", teacherNotes = "Gerçek haftalık senaryo", isChargeable = true
        }));
        return id;
    }

    private static async Task<PaymentRecord[]> Payments(WebApplicationFactory<Program> factory, Guid studentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().PaymentRecords.AsNoTracking().Where(p => p.StudentId == studentId).ToArrayAsync();
    }
    private static HttpClient Client(WebApplicationFactory<Program> factory, string ip)
    {
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Forwarded-For", ip); return client;
    }
    private static async Task<Guid> Register(HttpClient client, int role)
    {
        var json = await Json(await client.PostAsJsonAsync("/api/identity/register", new
        { email = $"lifecycle-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Yaşam", lastName = "Test", roles = new[] { role } }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return json.GetProperty("userId").GetGuid();
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var json = JsonDocument.Parse(body); return json.RootElement.Clone();
    }
    private static async Task Drain(WebApplicationFactory<Program> factory)
    {
        for (var i = 0; i < 30; i++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().DispatchPendingAsync() == 0) return;
        }
        Assert.Fail("Outbox boşalmadı.");
    }
}
