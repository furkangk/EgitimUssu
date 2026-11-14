using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P17 T1 / N-01 (K-20) — gerçek Postgres + gerçek HTTP: tekrarlı derste kapsam zorunlu.
/// <para>
/// Eskiden kapsamsız istek <c>All</c> sayılıyordu (istek DTO'sunda varsayılan): haftalık dersin "bu haftası" iptal edilince
/// bütün seri iptal oluyor, öğrencinin erteleme talebi kabul edilince bütün seri kayıyordu. Artık kapsamsız istek
/// <c>400 scheduling.scope_required</c>; talep hangi oluşumu kastettiğini taşır ve kabul yalnız onu taşır. DTO'da kalmış
/// bir varsayılanı ancak HTTP yakalar — testler bu yüzden uçtan uca.
/// </para>
/// <para>
/// Migration <c>AddChangeRequestOccurrence</c>: tekil derslerin eski taleplerini dersin başlangıcıyla geri doldurur,
/// tekrarlı derslerinkiler kurtarılamaz (hangi hafta?) → NULL. Boş test veritabanında geri doldurma hiç çalışmayacağı için
/// ayrı veritabanında kanıtlanır (Öğrenilenler #223 e).
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealOccurrenceScopeTests(ContainerFixture fixture)
{
    private const string MigrationBeforeOccurrence = "20261114194154_UniqueOccurrenceExceptions";

    [SkippableFact]
    public async Task Recurring_Cancel_Without_Scope_Is_400_And_Single_Cancel_Keeps_Neighbouring_Weeks()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherUserId, _, _, seriesId, monday) = await CreateWeeklyMondaySeriesAsync(factory, "10.184.0.1", linkStudent: false);

        var withoutScope = await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{seriesId}/cancel", new
        {
            reason = 1,
            isChargeable = false,
            cancellationNote = "Kapsamsiz",
        });
        Assert.Equal(HttpStatusCode.BadRequest, withoutScope.StatusCode);
        Assert.Equal("scheduling.scope_required", await ReadCodeAsync(withoutScope));

        var single = await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{seriesId}/cancel", new
        {
            reason = 1,
            isChargeable = false,
            cancellationNote = "Yalniz bu hafta",
            scope = 1, // OccurrenceScope.Single
            occurrenceStartAtUtc = monday.AddDays(7),
        });
        Assert.True(single.StatusCode == HttpStatusCode.OK, $"{(int)single.StatusCode}: {await single.Content.ReadAsStringAsync()}");

        var listed = await ListStartsAsync(teacher, teacherUserId, seriesId, monday.AddDays(-1), monday.AddDays(21));
        Assert.Equal(new[] { monday, monday.AddDays(14), monday.AddDays(21) }, listed);
    }

    [SkippableFact]
    public async Task Accepting_A_Students_Request_Moves_Exactly_That_Occurrence()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherUserId, student, studentId, seriesId, monday) = await CreateWeeklyMondaySeriesAsync(factory, "10.184.1.1", linkStudent: true);

        var target = monday.AddDays(7);
        var tuesday = target.AddDays(1);
        var request = await student!.PostAsJsonAsync($"/api/scheduling/students/{studentId}/lesson-requests", new
        {
            lessonScheduleId = seriesId,
            reason = "Sinavim var",
            proposedStartAtUtc = tuesday,
            proposedEndAtUtc = tuesday.AddHours(1),
            occurrenceStartAtUtc = target,
        });
        var requestBody = await request.Content.ReadAsStringAsync();
        Assert.True(request.StatusCode == HttpStatusCode.OK, $"{(int)request.StatusCode}: {requestBody}");
        Guid requestId;
        using (var document = JsonDocument.Parse(requestBody))
        {
            requestId = document.RootElement.GetProperty("id").GetGuid();
            Assert.Equal(target, document.RootElement.GetProperty("occurrenceStartAtUtc").GetDateTime().ToUniversalTime());
        }

        var accept = await teacher.PostAsync($"/api/scheduling/lesson-requests/{requestId}/accept", content: null);
        Assert.True(accept.StatusCode == HttpStatusCode.OK, $"{(int)accept.StatusCode}: {await accept.Content.ReadAsStringAsync()}");

        var listed = await ListStartsAsync(teacher, teacherUserId, seriesId, monday.AddDays(-1), monday.AddDays(21));
        Assert.Equal(new[] { monday, tuesday, monday.AddDays(14), monday.AddDays(21) }, listed);
    }

    [SkippableFact]
    public async Task Migration_Backfills_Single_Lesson_Requests_And_Leaves_Recurring_Ones_Unknown()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        var databaseName = $"change_request_migration_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = databaseName }.ConnectionString;
        await ExecuteAdminAsync($"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using var dbContext = NewSchedulingContext(connectionString);
            var migrator = dbContext.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationBeforeOccurrence);

            var singleLesson = Guid.NewGuid();
            var series = Guid.NewGuid();
            var singleStart = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);
            await InsertLessonAsync(dbContext, singleLesson, singleStart, recurrenceRule: null);
            await InsertLessonAsync(dbContext, series, singleStart.AddDays(-30), "FREQ=WEEKLY;BYDAY=FR");
            var singleRequest = Guid.NewGuid();
            var seriesRequest = Guid.NewGuid();
            await InsertRequestAsync(dbContext, singleRequest, singleLesson);
            await InsertRequestAsync(dbContext, seriesRequest, series);

            await migrator.MigrateAsync();

            var rows = await dbContext.LessonChangeRequests.AsNoTracking()
                .Select(request => new { request.Id, request.OccurrenceStartAtUtc })
                .ToDictionaryAsync(request => request.Id, request => request.OccurrenceStartAtUtc);
            Assert.Equal((DateTime?)singleStart, rows[singleRequest]?.ToUniversalTime());
            Assert.Null(rows[seriesRequest]);

            // Down: kolon düşer.
            await migrator.MigrateAsync(MigrationBeforeOccurrence);
            var column = await dbContext.Database.SqlQueryRaw<int>("""
                SELECT COUNT(*)::int AS "Value" FROM information_schema.columns
                WHERE table_schema = 'scheduling' AND table_name = 'lesson_change_requests' AND column_name = 'OccurrenceStartAtUtc'
                """).SingleAsync();
            Assert.Equal(0, column);
        }
        finally
        {
            await using (var pooled = new NpgsqlConnection(connectionString))
            {
                NpgsqlConnection.ClearPool(pooled);
            }

            await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static SchedulingDbContext NewSchedulingContext(string connectionString)
        => new(
            new DbContextOptionsBuilder<SchedulingDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", SchedulingDbContext.SchemaName))
                .Options,
            new NoOpDomainEventMapper());

    private static Task<int> InsertLessonAsync(SchedulingDbContext dbContext, Guid id, DateTime startAtUtc, string? recurrenceRule)
        => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO scheduling.lesson_schedules
                ("Id", "TeacherUserId", "StudentId", "Subject", "LessonFormat", "StartAtUtc", "EndAtUtc", "TimeZone",
                 "RecurrenceRule", "Status", "ReminderOffsetMinutes", "IsChargeable", "CreatedOnUtc", "UpdatedOnUtc")
            VALUES ({id}, {Guid.NewGuid()}, {Guid.NewGuid()}, 'Matematik', 'Online', {startAtUtc}, {startAtUtc.AddHours(1)},
                    'Europe/Istanbul', {recurrenceRule}, 'Planned', 60, FALSE, {startAtUtc.AddDays(-1)}, {startAtUtc.AddDays(-1)})
            """);

    private static Task<int> InsertRequestAsync(SchedulingDbContext dbContext, Guid id, Guid lessonId)
        => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO scheduling.lesson_change_requests
                ("Id", "LessonScheduleId", "StudentId", "TeacherUserId", "Reason", "Status", "CreatedOnUtc")
            VALUES ({id}, {lessonId}, {Guid.NewGuid()}, {Guid.NewGuid()}, 'Eski talep', 'Pending', {DateTime.UtcNow})
            """);

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Öğretmen + öğrenci profili (isteğe bağlı öğrenci hesabına bağlı) + haftalık Pazartesi 17:00 TR (14:00Z) serisi.</summary>
    private static async Task<(HttpClient Teacher, Guid TeacherUserId, HttpClient? Student, Guid StudentId, Guid SeriesId, DateTime Monday)>
        CreateWeeklyMondaySeriesAsync(WebApplicationFactory<Program> factory, string forwardedFor, bool linkStudent)
    {
        var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        var teacherUserId = await RegisterAsync(teacher, role: 2);
        var studentId = await ReadIdAsync(await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            userId = (Guid?)null,
            createdByTeacherUserId = teacherUserId,
            parentUserId = (Guid?)null,
            fullName = "Kapsam Ogrencisi",
            gradeLevel = "8. Sınıf",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            goalSummary = (string?)null,
            levelNotes = (string?)null,
            origin = 1,   // StudentOrigin.TeacherManaged
            subjects = Array.Empty<object>(),
        }));

        HttpClient? student = null;
        if (linkStudent)
        {
            student = factory.CreateClient();
            student.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor.Replace(".1", ".2", StringComparison.Ordinal));
            var studentUserId = await RegisterAsync(student, role: 3);
            await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherUserId, studentId, studentUserId);
        }

        var monday = DateTime.UtcNow.Date.AddDays(14);
        while (monday.DayOfWeek != DayOfWeek.Monday)
        {
            monday = monday.AddDays(1);
        }

        monday = DateTime.SpecifyKind(monday.AddHours(14), DateTimeKind.Utc);
        var seriesId = await ReadIdAsync(await teacher.PostAsJsonAsync("/api/scheduling/lessons", new
        {
            teacherUserId,
            studentId,
            subject = "Matematik",
            lessonFormat = 2,
            startAtUtc = monday,
            endAtUtc = monday.AddHours(1),
            timeZone = "Europe/Istanbul",
            recurrenceRule = "FREQ=WEEKLY;BYDAY=MO",
            reminderOffsetMinutes = 60,
            locationLabel = "Zoom",
            notes = (string?)null,
        }));

        return (teacher, teacherUserId, student, studentId, seriesId, monday);
    }

    private static async Task<DateTime[]> ListStartsAsync(HttpClient teacher, Guid teacherUserId, Guid lessonId, DateTime fromUtc, DateTime toUtc)
    {
        var path = $"/api/scheduling/teachers/{teacherUserId}/lessons"
            + $"?startAtUtc={Uri.EscapeDataString(fromUtc.ToString("O"))}&endAtUtc={Uri.EscapeDataString(toUtc.ToString("O"))}";
        var response = await teacher.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {body}");

        using var document = JsonDocument.Parse(body);
        return document.RootElement.EnumerateArray()
            .Where(item => item.GetProperty("id").GetGuid() == lessonId)
            .Select(item => item.GetProperty("startAtUtc").GetDateTime().ToUniversalTime())
            .OrderBy(start => start)
            .ToArray();
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, int role)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"kapsam-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = "Kapsam",
            lastName = "Test",
            phoneNumber = (string?)null,
            roles = new[] { role },
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var payload = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", payload.RootElement.GetProperty("accessToken").GetString());
        return payload.RootElement.GetProperty("userId").GetGuid();
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("id").GetGuid();
    }
}
