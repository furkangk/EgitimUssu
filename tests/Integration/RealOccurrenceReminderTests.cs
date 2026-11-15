using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Modules.Parents.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Push;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealOccurrenceReminderTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Reschedule_cancel_delete_and_late_scheduled_event_follow_current_occurrence()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infrastructure = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", "10.193.1.1");
        var teacherId = await Register(teacher, 2);
        var student = await Json(await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            createdByTeacherUserId = teacherId, fullName = "Hatırlatma Öğrencisi", gradeLevel = "8. Sınıf", origin = 1, subjects = Array.Empty<object>()
        }));
        var studentId = student.GetProperty("id").GetGuid();
        var start = DateTime.UtcNow.AddHours(3);
        var scheduleId = await Schedule(teacher, teacherId, studentId, start, "FREQ=WEEKLY;COUNT=3");
        await Drain(factory);
        var first = Assert.Single(await Reminders(factory, scheduleId));
        Assert.Equal(start, first.OccurrenceStartUtc);
        await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{scheduleId}/reschedule", new
            { newStartAtUtc = start.AddHours(3), newEndAtUtc = start.AddHours(4), scope = 1, occurrenceStartAtUtc = start }));
        await Drain(factory);
        var moved = Assert.Single(await Reminders(factory, scheduleId));
        Assert.Equal(first.Id, moved.Id);
        Assert.Equal(start, moved.OccurrenceStartUtc);
        Assert.Equal(start.AddHours(3).AddMinutes(-15), moved.RemindAtUtc);
        Assert.NotEqual(first.Message, moved.Message);
        await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{scheduleId}/cancel", new
            { reason = 4, isChargeable = false, scope = 1, occurrenceStartAtUtc = start }));
        await Drain(factory);
        Assert.Equal(ReminderStatus.Cancelled, Assert.Single(await Reminders(factory, scheduleId)).Status);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var message = await db.OutboxMessages.FirstAsync(m => m.Type == "LessonScheduledDomainEvent" && m.Payload.Contains(scheduleId.ToString()));
            var original = JsonSerializer.Deserialize<IntegrationEvent>(message.Payload, IntegrationEventSerialization.Options)!;
            var envelope = original with { EventId = Guid.NewGuid() };
            using var payload = JsonDocument.Parse(envelope.Payload);
            Assert.Equal(scheduleId, payload.RootElement.GetProperty("lessonScheduleId").GetGuid());
            var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler>().Where(h => h.CanHandle(envelope)).ToArray();
            Assert.Contains(handlers, h => h.GetType().Name == "LessonScheduleNotificationIntegrationEventHandler");
            foreach (var handler in handlers) await handler.HandleAsync(envelope);
            Assert.True(await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Set<InboxMessage>()
                .AnyAsync(m => m.EventId == envelope.EventId && m.Handler == "LessonScheduleNotificationIntegrationEventHandler"));
        }
        Assert.Equal(ReminderStatus.Cancelled, Assert.Single(await Reminders(factory, scheduleId)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/scheduling/lessons/{scheduleId}")).StatusCode);
        await Drain(factory);
        Assert.DoesNotContain(await Reminders(factory, scheduleId), r => r.Status == ReminderStatus.Pending);
        var past = await Schedule(teacher, teacherId, studentId, start.AddDays(-30), null);
        await Drain(factory);
        Assert.Empty(await Reminders(factory, past));
    }

    [SkippableFact]
    public async Task Concurrent_planners_use_unique_occurrence_recipient_key_and_split_replaces_future_series()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infrastructure = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", "10.193.2.1");
        var teacherId = await Register(teacher, 2);
        var studentId = (await Json(await teacher.PostAsJsonAsync("/api/students/profiles", new
        { createdByTeacherUserId = teacherId, fullName = "Seri Öğrencisi", gradeLevel = "8", origin = 1, subjects = Array.Empty<object>() }))).GetProperty("id").GetGuid();
        var start = new DateTime(DateTime.UtcNow.AddDays(2).AddSeconds(-1).Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc).AddTicks(1234560);
        var schedule = await Schedule(teacher, teacherId, studentId, start, "FREQ=DAILY;COUNT=10");
        await Task.WhenAll(Plan(factory), Plan(factory));
        var reminders = await Reminders(factory, schedule);
        Assert.Equal(6, reminders.Count(r => r.Status == ReminderStatus.Pending));
        var cutoff = start.AddDays(2);
        var split = await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{schedule}/reschedule", new
            { newStartAtUtc = cutoff.AddHours(2), newEndAtUtc = cutoff.AddHours(3), scope = 2, occurrenceStartAtUtc = cutoff }));
        var newId = split.GetProperty("id").GetGuid();
        await Drain(factory);
        Assert.Equal(2, (await Reminders(factory, schedule)).Count(r => r.Status == ReminderStatus.Pending));
        Assert.Equal(3, (await Reminders(factory, newId)).Count(r => r.Status == ReminderStatus.Pending));
        Assert.All((await Reminders(factory, schedule)).Where(r => r.OccurrenceStartUtc >= cutoff), r => Assert.Equal(ReminderStatus.Cancelled, r.Status));
    }

    [SkippableFact]
    public async Task Teacher_student_and_approved_opted_in_parent_receive_own_reminder_and_revocation_blocks_delivery()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infrastructure = RealInfrastructure.Use(fixture);
        var sender = new Sender();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IPushSender>(sender))));
        using var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", "10.193.3.1");
        var teacherId = await Register(teacher, 2);
        var studentId = (await Json(await teacher.PostAsJsonAsync("/api/students/profiles", new
        { createdByTeacherUserId = teacherId, fullName = "Alıcı Öğrencisi", gradeLevel = "8", origin = 1, subjects = Array.Empty<object>() }))).GetProperty("id").GetGuid();
        using var student = factory.CreateClient();
        student.DefaultRequestHeaders.Add("X-Forwarded-For", "10.193.3.2");
        var studentUser = await Register(student, 3);
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherId, studentId, studentUser);
        var parentUser = Guid.NewGuid();
        var disabledParent = Guid.NewGuid();
        var pendingParent = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var parents = scope.ServiceProvider.GetRequiredService<ParentsDbContext>();
            foreach (var id in new[] { parentUser, disabledParent, pendingParent })
            {
                var profile = new ParentProfile(Guid.NewGuid(), id, "Veli", null, null, now);
                profile.UpdateNotificationPreferences(true, true, id != disabledParent, true, true, ParentNotificationChannel.Push, now);
                parents.ParentProfiles.Add(profile);
                var link = new ParentChildLink(Guid.NewGuid(), id, studentId, "Öğrenci", null, null, false, now);
                if (id != pendingParent) link.Approve(id, null, now);
                parents.ParentChildLinks.Add(link);
            }
            await parents.SaveChangesAsync();
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            foreach (var id in new[] { teacherId, studentUser, parentUser, disabledParent, pendingParent })
                db.DeviceTokens.Add(new(Guid.NewGuid(), id, id.ToString(), DevicePlatform.Android, "Test", now));
            await db.SaveChangesAsync();
        }
        try
        {
        var start = now.AddMinutes(10);
        var schedule = await Schedule(teacher, teacherId, studentId, start, null);
        await Drain(factory);
        var reminders = await Reminders(factory, schedule);
        Assert.Equal(new[] { teacherId, studentUser, parentUser }.Order(), reminders.Select(r => r.RecipientUserId).Order());
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<INotificationDispatchProcessor>().DispatchDueRemindersAsync();
        Assert.Equal(new[] { teacherId.ToString(), studentUser.ToString(), parentUser.ToString() }.Order(), sender.Tokens.Order());
        sender.Tokens.Clear();
        await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{schedule}/reschedule", new
            { newStartAtUtc = start.AddMinutes(2), newEndAtUtc = start.AddHours(1).AddMinutes(2) }));
        await Drain(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var parents = scope.ServiceProvider.GetRequiredService<ParentsDbContext>();
            (await parents.ParentChildLinks.SingleAsync(l => l.ParentUserId == parentUser && l.StudentId == studentId)).Revoke(DateTime.UtcNow);
            await parents.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<INotificationDispatchProcessor>().DispatchDueRemindersAsync();
        }
        Assert.DoesNotContain(parentUser.ToString(), sender.Tokens);
        Assert.Equal(ReminderStatus.Cancelled, Assert.Single(await Reminders(factory, schedule), r => r.RecipientUserId == parentUser && r.ScheduledLessonStartAtUtc == start.AddMinutes(2)).Status);
        Assert.Equal(new[] { teacherId.ToString(), studentUser.ToString() }.Order(), sender.Tokens.Order());
        }
        finally
        {
        // Testin global bildirim işlerine bıraktığı aktif hedefleri temizle.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParentsDbContext>();
            await db.ParentChildLinks.Where(l => l.StudentId == studentId).ExecuteDeleteAsync();
            await db.ParentProfiles.Where(p => p.UserId == parentUser || p.UserId == disabledParent || p.UserId == pendingParent).ExecuteDeleteAsync();
        }
        }
    }

    private sealed class Sender : IPushSender
    {
        public List<string> Tokens { get; } = [];
        public Task<PushResult> SendAsync(string token, PushMessage message, CancellationToken cancellationToken = default)
        {
            Tokens.Add(token);
            return Task.FromResult(new PushResult(PushDeliveryStatus.Delivered, null));
        }
    }

    [SkippableFact]
    public async Task Migration_backfills_legacy_rows_and_downgrade_handles_multiple_recipients()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        var database = $"reminder_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(fixture.PostgresConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await command.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = database }.ConnectionString;
            var options = new DbContextOptionsBuilder<NotificationsDbContext>().UseNpgsql(connection).Options;
            await using var db = new NotificationsDbContext(options, new NoOpDomainEventMapper());
            var migrator = db.GetService<IMigrator>();
            const string previous = "20260905210125_AddOutboxTraceParent";
            await migrator.MigrateAsync(previous);
            var id = Guid.NewGuid(); var schedule = Guid.NewGuid(); var teacher = Guid.NewGuid(); var student = Guid.NewGuid();
            var start = new DateTime(2026, 11, 15, 15, 0, 0, DateTimeKind.Utc);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notifications.lesson_reminders
                ("Id", "LessonScheduleId", "TeacherUserId", "StudentId", "Title", "Message", "ScheduledLessonStartAtUtc", "RemindAtUtc", "Channel", "Status", "CreatedOnUtc", "UpdatedOnUtc", "AttemptCount")
                VALUES ({id}, {schedule}, {teacher}, {student}, 'Ders', 'Eski', {start}, {start}, 'Push', 'Pending', {start}, {start}, 0)
                """);
            await migrator.MigrateAsync();
            var row = await db.LessonReminders.SingleAsync();
            Assert.Equal(start, row.OccurrenceStartUtc);
            Assert.Equal(teacher, row.RecipientUserId);
            db.LessonReminders.Add(new(Guid.NewGuid(), schedule, teacher, student, "Ders", "Yeni", start, start, NotificationChannel.Push, ReminderStatus.Pending, start, start, Guid.NewGuid()));
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.LessonReminders.CountAsync());
            await migrator.MigrateAsync(previous);
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(teacher, (await db.LessonReminders.SingleAsync()).RecipientUserId);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task Plan(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LessonReminderPlanningProcessor>().RunAsync();
    }
    private static async Task<LessonReminder[]> Reminders(WebApplicationFactory<Program> factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().LessonReminders.AsNoTracking().Where(r => r.LessonScheduleId == id).ToArrayAsync();
    }
    private static async Task<Guid> Schedule(HttpClient teacher, Guid teacherId, Guid studentId, DateTime start, string? rule)
        => (await Json(await teacher.PostAsJsonAsync("/api/scheduling/lessons", new
        { teacherUserId = teacherId, studentId, subject = "Matematik", lessonFormat = 2, startAtUtc = start, endAtUtc = start.AddHours(1), timeZone = "Europe/Istanbul", recurrenceRule = rule, reminderOffsetMinutes = 15 }))).GetProperty("id").GetGuid();
    private static async Task<Guid> Register(HttpClient client, int role)
    {
        var body = await Json(await client.PostAsJsonAsync("/api/identity/register", new
        { email = $"reminder-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Ders", lastName = "Test", roles = new[] { role } }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return body.GetProperty("userId").GetGuid();
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
