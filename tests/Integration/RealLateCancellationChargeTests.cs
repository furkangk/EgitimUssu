using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Modules.LessonSessions.Infrastructure;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealLateCancellationChargeTests(ContainerFixture fixture)
{
    [SkippableTheory]
    [InlineData(3, true, 100)]
    [InlineData(48, true, 0)]
    [InlineData(3, false, 0)]
    public async Task Policy_controls_charge_and_cleans_only_cancelled_occurrence(int hours, bool allowCharge, decimal expected)
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infrastructure = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherId, studentId) = await Setup(factory);
        var start = Now.AddHours(hours);
        var scheduleId = await Schedule(teacher, teacherId, studentId, start, true);
        var sessionId = (await Json(await teacher.PostAsJsonAsync("/api/lesson-sessions/from-schedule", new
            { lessonScheduleId = scheduleId, occurrenceStartAtUtc = start }))).GetProperty("id").GetGuid();
        var nextId = (await Json(await teacher.PostAsJsonAsync("/api/lesson-sessions/from-schedule", new
            { lessonScheduleId = scheduleId, occurrenceStartAtUtc = start.AddDays(7) }))).GetProperty("id").GetGuid();
        await Drain(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            db.PaymentRecords.Add(new(Guid.NewGuid(), teacherId, studentId, sessionId, BillingItemType.LessonFee,
                "Bekleyen ders ücreti", "TRY", 100, 0, start.AddDays(7), null, PaymentStatus.Pending, null, null, null, Now));
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{scheduleId}/cancel",
            new { reason = 99, isChargeable = allowCharge, scope = 1, occurrenceStartAtUtc = start })).StatusCode);
        var premature = await teacher.PostAsJsonAsync($"/api/lesson-sessions/{sessionId}/complete", new
            { actualStartAtUtc = start, actualEndAtUtc = start.AddHours(1), attendanceStatus = 2, topicTitle = "Gelecek" });
        Assert.Equal(HttpStatusCode.BadRequest, premature.StatusCode);
        Assert.Contains("lesson_sessions.future_occurrence", await premature.Content.ReadAsStringAsync());
        await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{scheduleId}/cancel",
            new { reason = 2, isChargeable = allowCharge, scope = 1, occurrenceStartAtUtc = start }));
        await Drain(factory);
        // Aynı isteğin ikinci teslimi ve aynı oluşum iptalinin tekrarı yeni borç üretmez.
        await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{scheduleId}/cancel",
            new { reason = 2, isChargeable = allowCharge, scope = 1, occurrenceStartAtUtc = start }));
        await Drain(factory);
        await using var check = factory.Services.CreateAsyncScope();
        var payments = await check.ServiceProvider.GetRequiredService<PaymentsDbContext>().PaymentRecords.Where(p => p.StudentId == studentId).ToArrayAsync();
        Assert.Equal(PaymentStatus.Cancelled, Assert.Single(payments, p => p.ItemType == BillingItemType.LessonFee).Status);
        Assert.Equal(expected, payments.Where(p => p.ItemType == BillingItemType.LateCancellation).Sum(p => p.ExpectedAmount));
        Assert.Equal(expected > 0 ? 1 : 0, payments.Count(p => p.ItemType == BillingItemType.LateCancellation));
        var sessions = check.ServiceProvider.GetRequiredService<LessonSessionsDbContext>();
        Assert.Equal(LessonSessionStatus.Cancelled, (await sessions.LessonSessions.SingleAsync(s => s.Id == sessionId)).Status);
        Assert.Equal(LessonSessionStatus.Planned, (await sessions.LessonSessions.SingleAsync(s => s.Id == nextId)).Status);
        Assert.DoesNotContain(await check.ServiceProvider.GetRequiredService<NotificationsDbContext>().LessonReminders
            .Where(r => r.LessonScheduleId == scheduleId).ToArrayAsync(), r => r.Status == ReminderStatus.Pending);
    }

    [SkippableFact]
    public async Task Deleting_future_schedule_cleans_session_and_reminder()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infrastructure = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherId, studentId) = await Setup(factory);
        var start = Now.AddDays(3);
        var schedule = await Schedule(teacher, teacherId, studentId, start, false);
        await Json(await teacher.PostAsJsonAsync("/api/lesson-sessions/from-schedule", new { lessonScheduleId = schedule }));
        await Drain(factory);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/scheduling/lessons/{schedule}")).StatusCode);
        await Drain(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(LessonSessionStatus.Cancelled, (await scope.ServiceProvider.GetRequiredService<LessonSessionsDbContext>().LessonSessions.SingleAsync(s => s.LessonScheduleId == schedule)).Status);
        Assert.DoesNotContain(await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().LessonReminders.Where(r => r.LessonScheduleId == schedule).ToArrayAsync(), r => r.Status == ReminderStatus.Pending);
    }

    [SkippableFact]
    public async Task Only_one_pending_request_and_acceptance_rollback_are_atomic()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infrastructure = RealInfrastructure.Use(fixture);
        var failure = new FailAcceptanceSave();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.ConfigureDbContext<SchedulingDbContext>((_, options) => options.AddInterceptors(failure))));
        var (teacher, teacherId, studentId) = await Setup(factory);
        using var student = factory.CreateClient(); student.DefaultRequestHeaders.Add("X-Forwarded-For", "10.192.2.2");
        var studentUser = await Register(student, 3);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PutAsJsonAsync($"/api/teachers/profiles/{teacherId}/cancellation-policy", new { noticeHours = 0, chargePercent = 0 })).StatusCode);
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherId, studentId, studentUser);
        var start = Now.AddDays(3);
        var schedule = await Schedule(teacher, teacherId, studentId, start, true);
        var proposal = new { studentId, lessonScheduleId = schedule, reason = "Sınav", occurrenceStartAtUtc = start,
            proposedStartAtUtc = start.AddHours(3), proposedEndAtUtc = start.AddHours(4) };
        var responses = await Task.WhenAll(student.PostAsJsonAsync($"/api/scheduling/students/{studentId}/lesson-requests", proposal), student.PostAsJsonAsync($"/api/scheduling/students/{studentId}/lesson-requests", proposal));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var requestId = (await Json(responses.Single(r => r.StatusCode == HttpStatusCode.OK))).GetProperty("id").GetGuid();
        failure.Enabled = true;
        var failed = await teacher.PostAsync($"/api/scheduling/lesson-requests/{requestId}/accept", null);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        failure.Enabled = false;
        await using (var rollback = factory.Services.CreateAsyncScope())
        {
            var state = rollback.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            Assert.Equal(LessonChangeRequestStatus.Pending, (await state.LessonChangeRequests.SingleAsync(r => r.Id == requestId)).Status);
            Assert.False(await state.LessonOccurrenceExceptions.AnyAsync(e => e.SeriesLessonScheduleId == schedule));
        }
        await Json(await teacher.PostAsync($"/api/scheduling/lesson-requests/{requestId}/accept", null));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        Assert.Equal(LessonChangeRequestStatus.Accepted, (await db.LessonChangeRequests.SingleAsync(r => r.Id == requestId)).Status);
        Assert.Equal(start.AddHours(3), (await db.LessonOccurrenceExceptions.SingleAsync(e => e.SeriesLessonScheduleId == schedule)).OverrideStartAtUtc);
        // İptal edilmiş oluşuma yeni talep açılamaz.
        await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{schedule}/cancel", new { reason = 2, isChargeable = false, scope = 1, occurrenceStartAtUtc = start }));
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsJsonAsync($"/api/scheduling/students/{studentId}/lesson-requests", proposal)).StatusCode);
        var later = start.AddDays(7);
        var laterRequest = await Json(await student.PostAsJsonAsync($"/api/scheduling/students/{studentId}/lesson-requests",
            new { lessonScheduleId = schedule, reason = "Gelecek hafta", occurrenceStartAtUtc = later }));
        await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{schedule}/cancel", new { reason = 2, isChargeable = false, scope = 2, occurrenceStartAtUtc = later }));
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostAsync($"/api/scheduling/lesson-requests/{laterRequest.GetProperty("id").GetGuid()}/accept", null)).StatusCode);
        var past = await Schedule(teacher, teacherId, studentId, Now.AddDays(-3), false);
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsJsonAsync($"/api/scheduling/students/{studentId}/lesson-requests", new { studentId, lessonScheduleId = past, reason = "Geçmiş" })).StatusCode);
    }

    [SkippableFact]
    public async Task Pending_request_migration_keeps_earliest_and_rejects_duplicates_without_deleting_rows()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        var database = "cancellation_migration_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(fixture.PostgresConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = database, Pooling = false };
            await using var db = new SchedulingDbContext(new DbContextOptionsBuilder<SchedulingDbContext>().UseNpgsql(connection.ConnectionString).Options, new NoOpDomainEventMapper());
            var migrator = db.GetService<IMigrator>();
            var migrations = db.Database.GetMigrations().ToArray();
            var targetIndex = Array.FindIndex(migrations, name => name.EndsWith("_PendingOccurrenceChangeRequest", StringComparison.Ordinal));
            Assert.True(targetIndex > 0, "PendingOccurrenceChangeRequest ve önceki migration bulunmalı.");
            var beforePendingRequestUniqueness = migrations[targetIndex - 1];
            await migrator.MigrateAsync(beforePendingRequestUniqueness);
            var schedule = Guid.NewGuid(); var student = Guid.NewGuid(); var teacher = Guid.NewGuid();
            var first = new LessonChangeRequest(Guid.NewGuid(), schedule, student, teacher, "İlk", null, null, Now.AddDays(3), Now.AddHours(-2));
            var duplicate = new LessonChangeRequest(Guid.NewGuid(), schedule, student, teacher, "İkinci", null, null, Now.AddDays(3), Now.AddHours(-1));
            db.LessonChangeRequests.AddRange(first, duplicate);
            await db.SaveChangesAsync();
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.LessonChangeRequests.CountAsync());
            Assert.Equal(first.Id, (await db.LessonChangeRequests.SingleAsync(r => r.Status == LessonChangeRequestStatus.Pending)).Id);
            Assert.Equal(LessonChangeRequestStatus.Rejected, (await db.LessonChangeRequests.SingleAsync(r => r.Id == duplicate.Id)).Status);
            await migrator.MigrateAsync(beforePendingRequestUniqueness);
            Assert.Equal(2, await db.LessonChangeRequests.CountAsync());
            await migrator.MigrateAsync();
            Assert.Equal(1, await db.LessonChangeRequests.CountAsync(r => r.Status == LessonChangeRequestStatus.Pending));
        }
        finally { await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync(); }
    }

    private static async Task<(HttpClient, Guid, Guid)> Setup(WebApplicationFactory<Program> factory)
    {
        var teacher = factory.CreateClient(); teacher.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.192.{Random.Shared.Next(3, 250)}.1");
        var id = await Register(teacher, 2);
        await Json(await teacher.PostAsJsonAsync("/api/teachers/profiles", new { userId = id, fullName = "İptal Öğretmeni", subject = "Matematik",
            city = "İstanbul", district = "Kadıköy", lessonFormat = 2, experienceYears = 1, educationLevel = "Lisans", hourlyRateAmount = 100,
            currency = "TRY", availabilitySlots = Array.Empty<object>(), subjects = Array.Empty<string>(), certificates = Array.Empty<object>() }));
        var policy = await Json(await teacher.PutAsJsonAsync($"/api/teachers/profiles/{id}/cancellation-policy", new { noticeHours = 24, chargePercent = 100 }));
        Assert.Equal(24, policy.GetProperty("cancellationNoticeHours").GetInt32());
        using var stranger = factory.CreateClient(); stranger.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.193.{Random.Shared.Next(1, 250)}.1");
        await Register(stranger, 2);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PutAsJsonAsync($"/api/teachers/profiles/{id}/cancellation-policy", new { noticeHours = 0, chargePercent = 0 })).StatusCode);
        var profile = await Json(await teacher.PostAsJsonAsync("/api/students/profiles", new { createdByTeacherUserId = id, fullName = "İptal Öğrencisi", gradeLevel = "8. Sınıf", origin = 1, subjects = Array.Empty<object>() }));
        var student = profile.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PutAsJsonAsync($"/api/students/teachers/{id}/students/{student}/rate", new { agreedRateAmount = 100, currency = "TRY" })).StatusCode);
        return (teacher, id, student);
    }
    private static async Task<Guid> Schedule(HttpClient teacher, Guid teacherId, Guid studentId, DateTime start, bool recurring)
        => (await Json(await teacher.PostAsJsonAsync("/api/scheduling/lessons", new { teacherUserId = teacherId, studentId, subject = "Matematik", lessonFormat = 2,
            startAtUtc = start, endAtUtc = start.AddHours(1), timeZone = "Europe/Istanbul", recurrenceRule = recurring ? "FREQ=WEEKLY;COUNT=3" : null, reminderOffsetMinutes = 15 }))).GetProperty("id").GetGuid();
    private static async Task<Guid> Register(HttpClient client, int role)
    {
        var json = await Json(await client.PostAsJsonAsync("/api/identity/register", new { email = $"cancel-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "İptal", lastName = "Test", roles = new[] { role } }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return json.GetProperty("userId").GetGuid();
    }
    private sealed class FailAcceptanceSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<LessonChangeRequest>().Any(e => e.Entity.Status == LessonChangeRequestStatus.Accepted))
                throw new InvalidOperationException("Test: talep kabulü kaydedilemedi.");
            return ValueTask.FromResult(result);
        }
    }

    private static DateTime Now => new(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text); return json.RootElement.Clone();
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
