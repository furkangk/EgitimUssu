using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Text.Json;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealTeacherDashboardOccurrenceTests(ContainerFixture fixture)
{
    private static readonly DateTime Now = new(2026, 11, 14, 21, 30, 0, DateTimeKind.Utc);
    private sealed class Clock : IClock { public DateTime UtcNow { get; set; } = Now; }

    [SkippableFact]
    public async Task Cancelled_before_scheduled_stays_tombstoned_and_local_today_counts_weekly_occurrences()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infrastructure = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>().WithLocalNow(Now);
        var (teacher, teacherId, student) = await Setup(factory, "10.194.1.1");
        using (teacher)
        {
            var anchor = Now.AddDays(-7).AddMinutes(-15); // Haftanın tekrarı bugün 00:15 TR.
            var weekly = await Schedule(teacher, teacherId, student, anchor, "FREQ=WEEKLY;COUNT=5");
            await Plan(factory);
            Assert.Equal(1, (await Json(await teacher.GetAsync($"/api/reporting/teachers/{teacherId}/dashboard"))).GetProperty("todayLessonCount").GetInt32());
            // Henüz reporting inbox'a girmemiş Scheduled'dan önce Cancelled uygulanır.
            await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{weekly}/cancel", new
                { reason = 4, isChargeable = false, scope = 1, occurrenceStartAtUtc = anchor.AddDays(7) }));
            await Replay(factory, weekly, "LessonCancellationAppliedDomainEvent");
            var tombstone = Assert.Single(await Items(factory, teacherId), i => i.OccurrenceStartUtc == anchor.AddDays(7));
            Assert.True(tombstone.IsCancelled);
            await Replay(factory, weekly, "LessonScheduledDomainEvent");
            Assert.True(Assert.Single(await Items(factory, teacherId), i => i.OccurrenceStartUtc == anchor.AddDays(7)).IsCancelled);
            Assert.Equal(0, (await Json(await teacher.GetAsync($"/api/reporting/teachers/{teacherId}/dashboard"))).GetProperty("todayLessonCount").GetInt32());
            // Gelecek hafta hâlâ aktif. Ardından tüm seri iptali + bayat Scheduled.
            Assert.Contains(await Items(factory, teacherId), i => !i.IsCancelled && i.OccurrenceStartUtc == anchor.AddDays(14));
            await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{weekly}/cancel", new { reason = 4, isChargeable = false, scope = 3 }));
            await Replay(factory, weekly, "LessonScheduleCancelledDomainEvent");
            await Replay(factory, weekly, "LessonScheduledDomainEvent");
            Assert.All(await Items(factory, teacherId), i => Assert.True(i.IsCancelled));
            var before = (await Items(factory, teacherId)).Select(i => (i.ItemId, i.OccurrenceStartUtc, i.IsCancelled)).OrderBy(i => i.OccurrenceStartUtc).ToArray();
            await using (var scope = factory.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<ReportingDbContext>().TeacherDashboardProjections
                    .Where(p => p.TeacherUserId == teacherId).ExecuteDeleteAsync();
            await Replay(factory, weekly, "LessonScheduledDomainEvent");
            Assert.Equal(before, (await Items(factory, teacherId)).Select(i => (i.ItemId, i.OccurrenceStartUtc, i.IsCancelled)).OrderBy(i => i.OccurrenceStartUtc).ToArray());

        }
    }

    [SkippableFact]
    public async Task Reschedule_split_delete_parallel_refresh_and_daily_roll_follow_current_source()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infrastructure = RealInfrastructure.Use(fixture);
        var clock = new Clock();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ILocalCalendar>(new LocalCalendar("Europe/Istanbul", clock))));
        var (teacher, teacherId, student) = await Setup(factory, "10.194.2.1");
        using (teacher)
        {
            var anchor = new DateTime(2026, 11, 15, 14, 0, 0, DateTimeKind.Utc);
            var series = await Schedule(teacher, teacherId, student, anchor, "FREQ=DAILY;COUNT=30");
            await Task.WhenAll(Plan(factory), Plan(factory));
            Assert.Equal(15, (await Items(factory, teacherId)).Length);
            var key = anchor.AddDays(1);
            await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{series}/reschedule", new
                { newStartAtUtc = key.AddHours(2), newEndAtUtc = key.AddHours(3), scope = 1, occurrenceStartAtUtc = key }));
            await Replay(factory, series, "LessonOccurrenceRescheduledDomainEvent");
            Assert.Equal(key.AddHours(2), Assert.Single(await Items(factory, teacherId), i => i.OccurrenceStartUtc == key).OccursAtUtc);
            var cutoff = anchor.AddDays(2);
            var split = await Json(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{series}/reschedule", new
                { newStartAtUtc = cutoff.AddHours(3), newEndAtUtc = cutoff.AddHours(4), scope = 2, occurrenceStartAtUtc = cutoff }));
            var newId = split.GetProperty("id").GetGuid();
            await Replay(factory, series, "LessonSeriesSplitDomainEvent");
            var rows = await Items(factory, teacherId);
            Assert.Equal(2, rows.Count(i => i.ItemId == series && !i.IsCancelled));
            Assert.Equal(13, rows.Count(i => i.ItemId == newId && !i.IsCancelled));
            Assert.All(rows.Where(i => i.ItemId == series && i.OccurrenceStartUtc >= cutoff), i => Assert.True(i.IsCancelled));
            // Yeni olay olmadan bir hafta ilerleme: yeni haftanın satırları üretilir, eskiler budanır.
            clock.UtcNow = Now.AddDays(7);
            await Plan(factory);
            rows = await Items(factory, teacherId);
            Assert.Equal(16, rows.Count(i => i.ItemId == newId && !i.IsCancelled));
            Assert.DoesNotContain(rows, i => i.ItemId == series && !i.IsCancelled);
            Assert.All(rows, i => Assert.True(i.OccursAtUtc >= new DateTime(2026, 11, 20, 21, 0, 0, DateTimeKind.Utc)));
            Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/scheduling/lessons/{newId}")).StatusCode);
            await Replay(factory, newId, "LessonScheduleDeletedDomainEvent");
            Assert.All(await Items(factory, teacherId), i => Assert.True(i.IsCancelled));
            await Replay(factory, series, "LessonSeriesSplitDomainEvent");
            Assert.All(await Items(factory, teacherId), i => Assert.True(i.IsCancelled));
        }
    }

    [SkippableFact]
    public async Task Migration_backfills_legacy_keys_and_down_preserves_other_item_kinds()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        var database = $"dashboard_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(fixture.PostgresConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await command.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = database }.ConnectionString;
            await using var db = new ReportingDbContext(new DbContextOptionsBuilder<ReportingDbContext>().UseNpgsql(connection).Options, new NoOpDomainEventMapper());
            var migrator = db.GetService<IMigrator>();
            const string previous = "20260905210137_AddOutboxTraceParent";
            await migrator.MigrateAsync(previous);
            var teacher = Guid.NewGuid(); var series = Guid.NewGuid(); var assignment = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO reporting.teacher_dashboard_projections ("TeacherUserId", "LastAppliedEventId", "UpdatedOnUtc")
                VALUES ({teacher}, {Guid.Empty}, {Now});
                INSERT INTO reporting.teacher_dashboard_items ("TeacherUserId", "Kind", "ItemId", "OccursAtUtc", "Amount", "Currency")
                VALUES ({teacher}, 1, {series}, {Now}, NULL, NULL), ({teacher}, 2, {assignment}, NULL, NULL, NULL)
                """);
            await migrator.MigrateAsync();
            var projection = await db.TeacherDashboardProjections.Include(p => p.Items).SingleAsync();
            Assert.Equal(Now, Assert.Single(projection.Items, i => i.Kind == TeacherDashboardItemKind.Lesson).OccurrenceStartUtc);
            Assert.Equal(DateTime.UnixEpoch, Assert.Single(projection.Items, i => i.Kind == TeacherDashboardItemKind.OpenAssignment).OccurrenceStartUtc);
            projection.TrackLessonOccurrence(series, Now.AddDays(7), Now.AddDays(7), false);
            await db.SaveChangesAsync();
            Assert.Equal(3, await db.Set<TeacherDashboardItem>().CountAsync());
            await migrator.MigrateAsync(previous);
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            var restored = await db.TeacherDashboardProjections.Include(p => p.Items).SingleAsync();
            Assert.Equal(2, restored.Items.Count);
            Assert.Equal(1, restored.PendingAssignmentCount);
            Assert.Equal(Now.AddDays(7), Assert.Single(restored.Items, i => i.Kind == TeacherDashboardItemKind.Lesson).OccurrenceStartUtc);
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
        await scope.ServiceProvider.GetRequiredService<TeacherDashboardOccurrenceProcessor>().RunAsync();
    }
    private static async Task<TeacherDashboardItem[]> Items(WebApplicationFactory<Program> factory, Guid teacher)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ReportingDbContext>().Set<TeacherDashboardItem>()
            .AsNoTracking().Where(i => i.TeacherUserId == teacher && i.Kind == TeacherDashboardItemKind.Lesson).ToArrayAsync();
    }
    private static async Task Replay(WebApplicationFactory<Program> factory, Guid series, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var message = await db.OutboxMessages.OrderByDescending(m => m.OccurredOnUtc)
            .FirstAsync(m => m.Type == name && m.Payload.Contains(series.ToString()));
        var original = JsonSerializer.Deserialize<IntegrationEvent>(message.Payload, IntegrationEventSerialization.Options)!;
        var envelope = original with { EventId = Guid.NewGuid() };
        foreach (var handler in scope.ServiceProvider.GetServices<IIntegrationEventHandler>()
            .Where(h => h.GetType().Assembly == typeof(ReportingDbContext).Assembly && h.CanHandle(envelope)))
            await handler.HandleAsync(envelope);
    }
    private static async Task<(HttpClient Teacher, Guid TeacherId, Guid Student)> Setup(WebApplicationFactory<Program> factory, string ip)
    {
        var teacher = factory.CreateClient(); teacher.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        var registration = await Json(await teacher.PostAsJsonAsync("/api/identity/register", new
        { email = $"dashboard-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Pano", lastName = "Test", roles = new[] { 2 } }));
        teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registration.GetProperty("accessToken").GetString());
        var teacherId = registration.GetProperty("userId").GetGuid();
        var student = await Json(await teacher.PostAsJsonAsync("/api/students/profiles", new
        { createdByTeacherUserId = teacherId, fullName = "Pano Öğrencisi", gradeLevel = "8", origin = 1, subjects = Array.Empty<object>() }));
        return (teacher, teacherId, student.GetProperty("id").GetGuid());
    }
    private static async Task<Guid> Schedule(HttpClient teacher, Guid teacherId, Guid studentId, DateTime start, string? rule)
        => (await Json(await teacher.PostAsJsonAsync("/api/scheduling/lessons", new
        { teacherUserId = teacherId, studentId, subject = "Matematik", lessonFormat = 2, startAtUtc = start, endAtUtc = start.AddHours(1), timeZone = "Europe/Istanbul", recurrenceRule = rule, reminderOffsetMinutes = 15 }))).GetProperty("id").GetGuid();
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var json = JsonDocument.Parse(body); return json.RootElement.Clone();
    }
}
