using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P16 T3 / N-05, N-14 — gerçek Postgres + gerçek HTTP.
/// <para>
/// N-05: Ertelenmiş bir oluşum tekrar ertelenince/iptal edilince ikinci istisna satırı yazılıyordu; ardından öğretmen
/// listesi genişleticideki <c>ToDictionary</c> yüzünden kalıcı 500 dönüyordu. Artık yazma upsert'tir ve
/// <c>(SeriesLessonScheduleId, OriginalStartAtUtc)</c> benzersiz indeksle korunur: tek satır, son değer geçerli.
/// İkinci istek, listenin döndürdüğü <c>occurrenceStartAtUtc</c> ile yapılır — anahtar istemciye gidip geri gelir.
/// </para>
/// <para>
/// N-14: Pazartesi dersi gelecek haftanın Salı'sına ertelenince gelecek haftanın listesinde görünür, bu haftada görünmez.
/// </para>
/// <para>
/// Migration: <c>UniqueOccurrenceExceptions</c> benzersiz indeksten önce aynı anahtarlı tekrarları temizler (en son
/// <c>CreatedOnUtc</c> kalır). Boş test veritabanında bu SQL hiç çalışmayacağı için ayrı bir veritabanı önceki
/// migration'a kurulur, tekrarlar yazılır, sonra yükseltilir; ardından <c>Down</c> da denenir.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealOccurrenceExceptionTests(ContainerFixture fixture)
{
    /// <summary>Bu görevden önceki son Scheduling migration'ı (göç testinin başlangıç noktası).</summary>
    private const string MigrationBeforeUniqueIndex = "20260905210140_AddOutboxTraceParent";

    [SkippableFact]
    public async Task Rescheduling_And_Cancelling_The_Same_Occurrence_Keeps_One_Exception_And_List_Stays_200()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherUserId, lessonId, monday) = await CreateWeeklyMondaySeriesAsync(factory, "10.180.0.1", "Istisna");

        var secondMonday = monday.AddDays(7);
        var weekStart = LocalWeekStart(secondMonday);
        var weekEnd = weekStart.AddDays(7).AddTicks(-1);

        // 1. erteleme: ikinci Pazartesi → Salı.
        Assert.Equal(HttpStatusCode.OK, (await RescheduleAsync(teacher, lessonId, secondMonday, secondMonday.AddDays(1))).StatusCode);
        var afterFirst = await ListOccurrenceAsync(teacher, teacherUserId, lessonId, secondMonday, weekStart, weekEnd);
        Assert.Equal(secondMonday.AddDays(1), afterFirst.StartAtUtc);

        // 2. erteleme aynı oluşuma — anahtar listeden okunur (istemcinin gerçekte yaptığı gibi).
        Assert.Equal(HttpStatusCode.OK, (await RescheduleAsync(teacher, lessonId, afterFirst.OccurrenceStartAtUtc, secondMonday.AddDays(2))).StatusCode);
        var afterSecond = await ListOccurrenceAsync(teacher, teacherUserId, lessonId, secondMonday, weekStart, weekEnd);
        Assert.Equal(secondMonday.AddDays(2), afterSecond.StartAtUtc);

        var row = Assert.Single(await ReadExceptionsAsync(factory, lessonId));
        Assert.Equal(OccurrenceExceptionAction.Rescheduled, row.Action);
        Assert.Equal(secondMonday, row.OriginalStartAtUtc);
        Assert.Equal(secondMonday.AddDays(2), row.OverrideStartAtUtc);
        Assert.Equal(secondMonday.AddDays(2).AddHours(1), row.OverrideEndAtUtc);

        // Ertelenmiş oluşum sonra iptal: yine tek satır, Cancelled, eski erteleme saatleri temizlendi.
        Assert.Equal(HttpStatusCode.OK, (await CancelOccurrenceAsync(teacher, lessonId, afterSecond.OccurrenceStartAtUtc)).StatusCode);
        var cancelled = Assert.Single(await ReadExceptionsAsync(factory, lessonId));
        Assert.Equal(OccurrenceExceptionAction.Cancelled, cancelled.Action);
        Assert.Equal(row.Id, cancelled.Id);
        Assert.Null(cancelled.OverrideStartAtUtc);
        Assert.Null(cancelled.OverrideEndAtUtc);

        var listed = await ListAsync(teacher, teacherUserId, weekStart, weekEnd);
        Assert.DoesNotContain(listed, item => item.LessonId == lessonId && item.OccurrenceStartAtUtc == secondMonday);
    }

    [SkippableFact]
    public async Task Occurrence_Moved_To_Next_Weeks_Tuesday_Is_Listed_Next_Week_Only()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherUserId, lessonId, monday) = await CreateWeeklyMondaySeriesAsync(factory, "10.180.1.1", "Tasima");

        var tuesdayNextWeek = monday.AddDays(8);
        Assert.Equal(HttpStatusCode.OK, (await RescheduleAsync(teacher, lessonId, monday, tuesdayNextWeek)).StatusCode);

        var thisWeekStart = LocalWeekStart(monday);
        var thisWeek = await ListAsync(teacher, teacherUserId, thisWeekStart, thisWeekStart.AddDays(7).AddTicks(-1));
        Assert.DoesNotContain(thisWeek, item => item.LessonId == lessonId);

        var nextWeek = (await ListAsync(teacher, teacherUserId, thisWeekStart.AddDays(7), thisWeekStart.AddDays(14).AddTicks(-1)))
            .Where(item => item.LessonId == lessonId)
            .OrderBy(item => item.StartAtUtc)
            .ToArray();
        Assert.Equal(new[] { monday.AddDays(7), tuesdayNextWeek }, nextWeek.Select(item => item.StartAtUtc));
        // Taşınan oluşum orijinal anahtarını taşır: ikinci bir erteleme/iptal aynı satırı hedefler.
        Assert.Equal(monday, nextWeek[1].OccurrenceStartAtUtc);
    }

    [SkippableFact]
    public async Task First_Occurrence_Moved_Before_The_Series_Start_Is_Listed_In_That_Earlier_Week()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherUserId, lessonId, monday) = await CreateWeeklyMondaySeriesAsync(factory, "10.180.2.1", "OneAlma");

        // İlk ders bir önceki haftanın Cuma'sına öne alındı: pencere serinin çapasından önce biter, seri yine yüklenmeli.
        var fridayBefore = monday.AddDays(-3);
        Assert.Equal(HttpStatusCode.OK, (await RescheduleAsync(teacher, lessonId, monday, fridayBefore)).StatusCode);

        var weekBefore = LocalWeekStart(monday).AddDays(-7);
        var moved = Assert.Single(
            await ListAsync(teacher, teacherUserId, weekBefore, weekBefore.AddDays(7).AddTicks(-1)),
            item => item.LessonId == lessonId);
        Assert.Equal(fridayBefore, moved.StartAtUtc);
        Assert.Equal(monday, moved.OccurrenceStartAtUtc);

        var seriesWeek = await ListAsync(teacher, teacherUserId, LocalWeekStart(monday), LocalWeekStart(monday).AddDays(7).AddTicks(-1));
        Assert.DoesNotContain(seriesWeek, item => item.LessonId == lessonId);
    }

    [SkippableFact]
    public async Task Migration_Removes_Duplicate_Exceptions_Keeping_The_Latest_And_Down_Reverts_The_Unique_Index()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        var databaseName = $"occurrence_migration_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = databaseName }.ConnectionString;
        await ExecuteAdminAsync($"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using (var dbContext = NewSchedulingContext(connectionString))
            {
                var migrator = dbContext.GetService<IMigrator>();
                await migrator.MigrateAsync(MigrationBeforeUniqueIndex);

                var seriesId = Guid.NewGuid();
                var otherSeriesId = Guid.NewGuid();
                var original = new DateTime(2026, 11, 16, 14, 0, 0, DateTimeKind.Utc);
                var oldest = Guid.NewGuid();
                var latest = Guid.NewGuid();
                var middle = Guid.NewGuid();
                var other = Guid.NewGuid();
                await InsertExceptionAsync(dbContext, oldest, seriesId, original, "Rescheduled", original.AddDays(-2));
                await InsertExceptionAsync(dbContext, latest, seriesId, original, "Cancelled", original.AddDays(-1));
                await InsertExceptionAsync(dbContext, middle, seriesId, original, "Rescheduled", original.AddDays(-1).AddHours(-1));
                await InsertExceptionAsync(dbContext, other, otherSeriesId, original, "Rescheduled", original.AddDays(-2));

                await migrator.MigrateAsync();

                var survivors = await dbContext.LessonOccurrenceExceptions.AsNoTracking()
                    .Select(x => new { x.Id, x.SeriesLessonScheduleId })
                    .ToArrayAsync();
                Assert.Equal(2, survivors.Length);
                Assert.Equal(latest, Assert.Single(survivors, x => x.SeriesLessonScheduleId == seriesId).Id);
                Assert.Equal(other, Assert.Single(survivors, x => x.SeriesLessonScheduleId == otherSeriesId).Id);

                var duplicate = await Assert.ThrowsAsync<PostgresException>(() =>
                    InsertExceptionAsync(dbContext, Guid.NewGuid(), seriesId, original, "Rescheduled", original));
                Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);

                // Down: indeks yine var ama benzersiz değil — aynı anahtarla ikinci satır yazılabilir.
                await migrator.MigrateAsync(MigrationBeforeUniqueIndex);
                await InsertExceptionAsync(dbContext, Guid.NewGuid(), seriesId, original, "Rescheduled", original);
                Assert.Equal(2, await dbContext.LessonOccurrenceExceptions.CountAsync(x => x.SeriesLessonScheduleId == seriesId));
            }
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

    private static Task<int> InsertExceptionAsync(
        SchedulingDbContext dbContext, Guid id, Guid seriesId, DateTime originalStartAtUtc, string action, DateTime createdOnUtc)
        => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO scheduling.lesson_occurrence_exceptions
                ("Id", "SeriesLessonScheduleId", "OriginalStartAtUtc", "Action", "OverrideStartAtUtc", "OverrideEndAtUtc", "Note", "CreatedOnUtc")
            VALUES ({id}, {seriesId}, {originalStartAtUtc}, {action}, NULL, NULL, NULL, {createdOnUtc})
            """);

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Öğretmen + öğretmenin yönettiği öğrenci + haftalık Pazartesi 17:00 TR (14:00Z) serisi. İlk Pazartesi en az iki hafta
    /// sonradır; yerel hafta sınırı (Pazartesi 00:00 TR = Pazar 21:00Z) ile UTC günü karışmasın diye ders 14:00Z'dedir.
    /// </summary>
    private static async Task<(HttpClient Teacher, Guid TeacherUserId, Guid LessonId, DateTime Monday)> CreateWeeklyMondaySeriesAsync(
        WebApplicationFactory<Program> factory, string forwardedFor, string label)
    {
        var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        var teacherUserId = await RegisterTeacherAsync(teacher, label);
        var studentId = await ReadIdAsync(await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            userId = (Guid?)null,
            createdByTeacherUserId = teacherUserId,
            parentUserId = (Guid?)null,
            fullName = $"{label} Ogrenci",
            gradeLevel = "8. Sınıf",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            goalSummary = (string?)null,
            levelNotes = (string?)null,
            origin = 1,   // StudentOrigin.TeacherManaged
            subjects = Array.Empty<object>(),
        }));

        var monday = DateTime.UtcNow.Date.AddDays(14);
        while (monday.DayOfWeek != DayOfWeek.Monday)
        {
            monday = monday.AddDays(1);
        }

        monday = DateTime.SpecifyKind(monday.AddHours(14), DateTimeKind.Utc);
        var lessonId = await ReadIdAsync(await teacher.PostAsJsonAsync("/api/scheduling/lessons", new
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

        return (teacher, teacherUserId, lessonId, monday);
    }

    /// <summary>Oluşumun yerel (TR) haftasının başı: Pazartesi 00:00 TR = önceki Pazar 21:00Z.</summary>
    private static DateTime LocalWeekStart(DateTime mondayUtc)
        => DateTime.SpecifyKind(mondayUtc.Date.AddHours(-3), DateTimeKind.Utc);

    private static Task<HttpResponseMessage> RescheduleAsync(HttpClient teacher, Guid lessonId, DateTime occurrenceStartAtUtc, DateTime newStartAtUtc)
        => teacher.PostAsJsonAsync($"/api/scheduling/lessons/{lessonId}/reschedule", new
        {
            newStartAtUtc,
            newEndAtUtc = newStartAtUtc.AddHours(1),
            note = "Ertelendi",
            scope = 1, // OccurrenceScope.Single
            occurrenceStartAtUtc,
        });

    private static Task<HttpResponseMessage> CancelOccurrenceAsync(HttpClient teacher, Guid lessonId, DateTime occurrenceStartAtUtc)
        => teacher.PostAsJsonAsync($"/api/scheduling/lessons/{lessonId}/cancel", new
        {
            reason = 1,
            isChargeable = false,
            cancellationNote = "Iptal",
            scope = 1, // OccurrenceScope.Single
            occurrenceStartAtUtc,
        });

    private static async Task<Listed> ListOccurrenceAsync(
        HttpClient teacher, Guid teacherUserId, Guid lessonId, DateTime occurrenceStartAtUtc, DateTime fromUtc, DateTime toUtc)
        => Assert.Single(
            await ListAsync(teacher, teacherUserId, fromUtc, toUtc),
            item => item.LessonId == lessonId && item.OccurrenceStartAtUtc == occurrenceStartAtUtc);

    /// <summary>Öğretmen listesini okur ve önce 200'ü doğrular — N-05'in belirtisi tam olarak buradaki 500'dü.</summary>
    private static async Task<Listed[]> ListAsync(HttpClient teacher, Guid teacherUserId, DateTime fromUtc, DateTime toUtc)
    {
        var path = $"/api/scheduling/teachers/{teacherUserId}/lessons"
            + $"?startAtUtc={Uri.EscapeDataString(fromUtc.ToString("O"))}&endAtUtc={Uri.EscapeDataString(toUtc.ToString("O"))}";
        var response = await teacher.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {body}");

        using var document = JsonDocument.Parse(body);
        return document.RootElement.EnumerateArray()
            .Select(item => new Listed(
                item.GetProperty("id").GetGuid(),
                item.GetProperty("startAtUtc").GetDateTime().ToUniversalTime(),
                item.GetProperty("occurrenceStartAtUtc").GetDateTime().ToUniversalTime()))
            .ToArray();
    }

    private static async Task<LessonOccurrenceException[]> ReadExceptionsAsync(WebApplicationFactory<Program> factory, Guid lessonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().LessonOccurrenceExceptions
            .AsNoTracking()
            .Where(x => x.SeriesLessonScheduleId == lessonId)
            .ToArrayAsync();
    }

    private static async Task<Guid> RegisterTeacherAsync(HttpClient client, string label)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"istisna-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = label,
            lastName = "Test",
            phoneNumber = (string?)null,
            roles = new[] { 2 },
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var payload = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", payload.RootElement.GetProperty("accessToken").GetString());
        return payload.RootElement.GetProperty("userId").GetGuid();
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private sealed record Listed(Guid LessonId, DateTime StartAtUtc, DateTime OccurrenceStartAtUtc);
}
