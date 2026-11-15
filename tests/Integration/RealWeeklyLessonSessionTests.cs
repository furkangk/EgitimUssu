using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.LessonSessions.Infrastructure;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealWeeklyLessonSessionTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Weekly_occurrences_are_distinct_idempotent_and_rescheduled_start_preserves_original_key()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherId, studentId, scheduleId, anchor) = await SetupAsync(factory, "10.190.0.1");
        var first = await JsonAsync(await DeriveAsync(teacher, scheduleId, anchor));
        var firstId = first.GetProperty("id").GetGuid();
        Assert.Equal(anchor, first.GetProperty("occurrenceStartAtUtc").GetDateTime());
        await CompleteAsync(teacher, firstId, anchor);
        var second = await JsonAsync(await DeriveAsync(teacher, scheduleId, anchor.AddDays(7)));
        var secondId = second.GetProperty("id").GetGuid();
        Assert.NotEqual(firstId, secondId);
        Assert.Equal(anchor.AddDays(7), second.GetProperty("plannedStartAtUtc").GetDateTime());
        Assert.Equal(secondId, (await JsonAsync(await DeriveAsync(teacher, scheduleId, anchor.AddDays(7)))).GetProperty("id").GetGuid());
        var thirdKey = anchor.AddDays(14);
        Assert.Equal(HttpStatusCode.OK, (await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{scheduleId}/reschedule", new
        {
            newStartAtUtc = thirdKey.AddHours(2), newEndAtUtc = thirdKey.AddHours(3), scope = 1,
            occurrenceStartAtUtc = thirdKey, note = "İki saat sonra"
        })).StatusCode);
        var third = await JsonAsync(await DeriveAsync(teacher, scheduleId, thirdKey));
        Assert.Equal(thirdKey, third.GetProperty("occurrenceStartAtUtc").GetDateTime());
        Assert.Equal(thirdKey.AddHours(2), third.GetProperty("plannedStartAtUtc").GetDateTime());
        Assert.Equal(HttpStatusCode.BadRequest, (await DeriveAsync(teacher, scheduleId, thirdKey.AddHours(2))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await DeriveAsync(teacher, scheduleId, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostAsync($"/api/lesson-sessions/from-schedule/{scheduleId}", null)).StatusCode);
        // İki eşzamanlı ilk istek de aynı kalıcı oturumu döndürür; outbox yalnız bir kayıt üretir.
        var parallelKey = anchor.AddDays(21);
        var parallel = await Task.WhenAll(DeriveAsync(teacher, scheduleId, parallelKey), DeriveAsync(teacher, scheduleId, parallelKey));
        var ids = await Task.WhenAll(parallel.Select(async response => (await JsonAsync(response)).GetProperty("id").GetGuid()));
        Assert.Equal(ids[0], ids[1]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LessonSessionsDbContext>();
        Assert.Equal(4, await db.LessonSessions.CountAsync(item => item.LessonScheduleId == scheduleId));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(item => item.Type == "LessonSessionCreatedDomainEvent" && item.Payload.Contains(ids[0].ToString())));
        await DrainAsync(factory);
        await scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>().TopicMasteries.Where(item => item.StudentId == studentId).ExecuteDeleteAsync();
    }

    [SkippableFact]
    public async Task Student_and_approved_parent_read_own_sessions_but_foreign_and_revoked_parent_cannot()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherId, studentId, scheduleId, anchor) = await SetupAsync(factory, "10.190.1.1");
        var sessionId = (await JsonAsync(await DeriveAsync(teacher, scheduleId, anchor))).GetProperty("id").GetGuid();
        using var student = Client(factory, "10.190.1.2");
        var studentUserId = await RegisterAsync(student, 3);
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherId, studentId, studentUserId);
        var studentList = await JsonAsync(await student.GetAsync("/api/lesson-sessions"));
        Assert.Contains(studentList.EnumerateArray(), item => item.GetProperty("id").GetGuid() == sessionId);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/api/lesson-sessions/{sessionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/api/lesson-sessions?studentId={studentId}")).StatusCode);
        using var parent = Client(factory, "10.190.1.3");
        await RegisterAsync(parent, 4);
        using var stranger = Client(factory, "10.190.1.4");
        await RegisterAsync(stranger, 3);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/lesson-sessions/{sessionId}")).StatusCode);
        Assert.Empty((await JsonAsync(await stranger.GetAsync("/api/lesson-sessions"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync($"/api/lesson-sessions/{sessionId}")).StatusCode);
        var invite = await JsonAsync(await teacher.PostAsJsonAsync($"/api/students/profiles/{studentId}/parent-invite", new { teacherUserId = teacherId }));
        var claim = await JsonAsync(await parent.PostAsJsonAsync("/api/parents/children/claim-invite", new { inviteCode = invite.GetProperty("inviteCode").GetString() }));
        await DrainAsync(factory);
        var parentList = await JsonAsync(await parent.GetAsync("/api/lesson-sessions"));
        Assert.Contains(parentList.EnumerateArray(), item => item.GetProperty("id").GetGuid() == sessionId);
        Assert.Equal(HttpStatusCode.OK, (await parent.GetAsync($"/api/lesson-sessions/{sessionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync($"/api/lesson-sessions?studentId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await parent.PostAsync($"/api/parents/children/{claim.GetProperty("id").GetGuid()}/revoke", null)).StatusCode);
        Assert.Empty((await JsonAsync(await parent.GetAsync("/api/lesson-sessions"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync($"/api/lesson-sessions/{sessionId}")).StatusCode);
        // Oluşum yazma yetkisi sahipliktir; başka öğretmen ve bağsız öğrenci türetemez.
        using var otherTeacher = Client(factory, "10.190.1.5");
        await RegisterAsync(otherTeacher, 2);
        Assert.Equal(HttpStatusCode.Forbidden, (await DeriveAsync(otherTeacher, scheduleId, anchor.AddDays(7))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await DeriveAsync(stranger, scheduleId, anchor.AddDays(7))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync($"/api/students/teachers/{teacherId}/students/{studentId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await DeriveAsync(teacher, scheduleId, anchor.AddDays(7))).StatusCode); // #218 mevcut kaydı kapatma yetkisi
    }

    [SkippableFact]
    public async Task Recompletion_emits_one_event_and_absent_is_not_attendance_in_real_postgres()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, _, studentId, scheduleId, anchor) = await SetupAsync(factory, "10.190.2.1");
        try
        {
            var firstId = (await JsonAsync(await DeriveAsync(teacher, scheduleId, anchor))).GetProperty("id").GetGuid();
            await CompleteAsync(teacher, firstId, anchor);
            await DrainAsync(factory);
            await CompleteAsync(teacher, firstId, anchor);
            var absent = (await JsonAsync(await DeriveAsync(teacher, scheduleId, anchor.AddDays(7)))).GetProperty("id").GetGuid();
            await CompleteAsync(teacher, absent, anchor.AddDays(7), 4);
            await DrainAsync(factory);
            await using var scope = factory.Services.CreateAsyncScope();
            var sessions = scope.ServiceProvider.GetRequiredService<LessonSessionsDbContext>();
            Assert.Equal(1, await sessions.OutboxMessages.CountAsync(item => item.Type == "LessonSessionCompletedDomainEvent" && item.Payload.Contains(firstId.ToString())));
            var progress = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
            Assert.Equal(1, await progress.TopicMasteries.Where(item => item.StudentId == studentId).SumAsync(item => item.AttendedLessonCount));
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>().TopicMasteries.Where(item => item.StudentId == studentId).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task Migration_backfills_and_cancels_duplicates_before_unique_index_and_down_keeps_rows()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        var name = $"session_migration_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = name }.ConnectionString;
        await AdminAsync($"CREATE DATABASE \"{name}\"");
        try
        {
            await using var db = new LessonSessionsDbContext(new DbContextOptionsBuilder<LessonSessionsDbContext>()
                .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "lesson_sessions")).Options,
                new NoOpDomainEventMapper());
            var migrator = db.GetService<IMigrator>();
            const string previous = "20260905210116_AddOutboxTraceParent";
            await migrator.MigrateAsync(previous);
            var schedule = Guid.NewGuid();
            var earliest = Guid.NewGuid();
            var later = Guid.NewGuid();
            var manual = Guid.NewGuid();
            var at = new DateTime(2026, 11, 16, 14, 0, 0, DateTimeKind.Utc);
            await InsertLegacyAsync(db, earliest, schedule, at, at.AddDays(-2));
            await InsertLegacyAsync(db, later, schedule, at, at.AddDays(-1));
            await InsertLegacyAsync(db, manual, null, at, at);
            await migrator.MigrateAsync();
            var rows = await db.LessonSessions.AsNoTracking().ToArrayAsync();
            Assert.Equal(at, rows.Single(item => item.Id == earliest).OccurrenceStartUtc);
            Assert.Null(rows.Single(item => item.Id == later).OccurrenceStartUtc);
            Assert.Equal("Cancelled", rows.Single(item => item.Id == later).Status.ToString());
            Assert.Null(rows.Single(item => item.Id == manual).OccurrenceStartUtc);
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE lesson_sessions.lesson_sessions SET \"OccurrenceStartUtc\" = {at} WHERE \"Id\" = {later}"));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
            await migrator.MigrateAsync(previous);
            await InsertLegacyAsync(db, Guid.NewGuid(), schedule, at, at);
            var count = await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM lesson_sessions.lesson_sessions").SingleAsync();
            Assert.Equal(4, count);
        }
        finally
        {
            await using var connection = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(connection);
            await AdminAsync($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    private static Task<int> InsertLegacyAsync(LessonSessionsDbContext db, Guid id, Guid? schedule, DateTime planned, DateTime created)
        => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO lesson_sessions.lesson_sessions
                ("Id", "LessonScheduleId", "TeacherUserId", "StudentId", "Subject", "PlannedStartAtUtc",
                 "AttendanceStatus", "Status", "TopicTitle", "CreatedOnUtc", "IsChargeable")
            VALUES ({id}, {schedule}, {Guid.NewGuid()}, {Guid.NewGuid()}, 'Matematik', {planned}, 'Unknown', 'Planned', 'Konu', {created}, false)
            """);

    private async Task AdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(HttpClient Teacher, Guid TeacherId, Guid StudentId, Guid ScheduleId, DateTime Anchor)> SetupAsync(WebApplicationFactory<Program> factory, string ip)
    {
        var teacher = Client(factory, ip);
        var teacherId = await RegisterAsync(teacher, 2);
        var profile = await JsonAsync(await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            createdByTeacherUserId = teacherId, fullName = "Oluşum Öğrencisi", gradeLevel = "8. Sınıf", origin = 1,
            subjects = Array.Empty<object>()
        }));
        var studentId = profile.GetProperty("id").GetGuid();
        var anchor = new DateTime(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc);
        var lesson = await JsonAsync(await teacher.PostAsJsonAsync("/api/scheduling/lessons", new
        {
            teacherUserId = teacherId, studentId, subject = "Matematik", lessonFormat = 2,
            startAtUtc = anchor, endAtUtc = anchor.AddHours(1), timeZone = "Europe/Istanbul",
            recurrenceRule = "FREQ=WEEKLY;BYDAY=MO;COUNT=5", reminderOffsetMinutes = 60
        }));
        return (teacher, teacherId, studentId, lesson.GetProperty("id").GetGuid(), anchor);
    }

    private static Task<HttpResponseMessage> DeriveAsync(HttpClient teacher, Guid schedule, DateTime? occurrence)
        => teacher.PostAsJsonAsync("/api/lesson-sessions/from-schedule", new { lessonScheduleId = schedule, occurrenceStartAtUtc = occurrence });

    private static async Task CompleteAsync(HttpClient teacher, Guid session, DateTime start, int attendance = 2)
        => await JsonAsync(await teacher.PostAsJsonAsync($"/api/lesson-sessions/{session}/complete", new
        {
            actualStartAtUtc = start, actualEndAtUtc = start.AddHours(1), attendanceStatus = attendance,
            topicTitle = "Kesirler", teacherNotes = "Not", isChargeable = false
        }));

    private static HttpClient Client(WebApplicationFactory<Program> factory, string ip)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, int role)
    {
        var json = await JsonAsync(await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"weekly-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Oluşum", lastName = "Test", roles = new[] { role }
        }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return json.GetProperty("userId").GetGuid();
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.Clone();
    }

    private static async Task DrainAsync(WebApplicationFactory<Program> factory)
    {
        for (var round = 0; round < 30; round++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().DispatchPendingAsync() == 0) return;
        }
        Assert.Fail("Outbox boşalmadı.");
    }
}
