using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P16 T6 — kapanış senaryosu, gerçek Postgres + gerçek HTTP: gece yarısından hemen sonra gün **ve** ay aynı anda döner.
/// <para>
/// "Şimdi" Salı 1 Eylül 2026 00:45 TR (31 Ağustos 21:45Z) — UTC'de hâlâ Pazartesi 31 Ağustos. Öğretmenin Salı 00:30 TR
/// haftalık dersi (Ağustos'ta başlamış seri) panelde "bugün"dür (P16 T5), öğrencinin takviminde Salı'dır (T2: <c>BYDAY</c>
/// yerel gün), tamamlanınca aylık raporda Eylül'e yazılır (T5: yerel ay kovası). Aynı UTC gününün öğleden sonrasındaki
/// Pazartesi dersi "bugün" değildir — UTC gününe geri dönen bir hata bu satırla yakalanır.
/// </para>
/// <para>
/// Sınır: ders tamamlanma olayının anı sunucunun gerçek saatidir (<c>LessonSessionFeatures</c>, <c>IClock</c>); yerel
/// takvim sabitlenebilir ama <c>IClock</c> sabitlenemez (JWT süresi gerçek saate bakar). Bu yüzden ay sınırındaki
/// tamamlanma, Reporting tüketicilerine <c>RealProjectionRebuildTests</c>'teki yolla (yalnız Reporting handler'ları,
/// olay başına scope) teslim edilir.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealMidnightScenarioTests(ContainerFixture fixture)
{
    private static readonly DateTime TuesdaySeptemberFirst0045 = new(2026, 8, 31, 21, 45, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    [SkippableFact]
    public async Task Tuesday_0030_Weekly_Lesson_Is_Today_On_Tuesday_And_Lands_In_September()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>().WithLocalNow(TuesdaySeptemberFirst0045);

        var teacher = NewClient(factory, "10.183.0.1");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "GeceOgretmen");
        var student = NewClient(factory, "10.183.0.2");
        var studentUserId = await RegisterAsync(student, role: 3, "GeceOgrenci");
        var studentId = await ReadIdAsync(await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            userId = (Guid?)null,
            createdByTeacherUserId = teacherUserId,
            parentUserId = (Guid?)null,
            fullName = "Gece Ogrencisi",
            gradeLevel = "8. Sınıf",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            goalSummary = (string?)null,
            levelNotes = (string?)null,
            origin = 1,   // StudentOrigin.TeacherManaged
            subjects = Array.Empty<object>(),
        }));
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherUserId, studentId, studentUserId);

        // Salı 18 Ağustos 00:30 TR (17 Ağustos 21:30Z) çapalı haftalık Salı serisi + aynı UTC gününün (31 Ağustos)
        // öğleden sonrasında, yani yerelde Pazartesi'de, tekil bir ders.
        var seriesId = await ReadIdAsync(await CreateLessonAsync(
            teacher, teacherUserId, studentId, Utc(2026, 8, 17, 21, 30), "FREQ=WEEKLY;BYDAY=TU"));
        var mondayAfternoonId = await ReadIdAsync(await CreateLessonAsync(
            teacher, teacherUserId, studentId, Utc(2026, 8, 31, 12, 0), null));
        var septemberFirstOccurrence = Utc(2026, 8, 31, 21, 30);

        // 1) Panel: bugün yalnız Salı 00:30 TR oluşumu.
        using (var summary = await GetJsonAsync(teacher, $"/api/teachers/profiles/{teacherUserId}/dashboard-summary"))
        {
            var today = summary.RootElement.GetProperty("todayLessons").EnumerateArray()
                .Select(item => (Id: item.GetProperty("id").GetGuid(), Start: item.GetProperty("startAtUtc").GetDateTime().ToUniversalTime()))
                .ToArray();
            Assert.Equal(new[] { (seriesId, septemberFirstOccurrence) }, today);
            Assert.DoesNotContain(today, item => item.Id == mondayAfternoonId);
        }

        // 2) Öğrenci takvimi, Salı 1 Eylül'ün yerel günü [31 Ağu 21:00Z, 1 Eyl 21:00Z): seri oluşumu Salı'dır.
        using (var calendar = await GetJsonAsync(student,
            $"/api/scheduling/students/{studentId}/calendar?startAtUtc={Iso(Utc(2026, 8, 31, 21, 0))}&endAtUtc={Iso(Utc(2026, 9, 1, 20, 59))}"))
        {
            var occurrence = Assert.Single(calendar.RootElement.EnumerateArray());
            var start = occurrence.GetProperty("startAtUtc").GetDateTime().ToUniversalTime();
            Assert.Equal(septemberFirstOccurrence, start);
            Assert.Equal("Teacher", occurrence.GetProperty("source").GetString());
            var local = TimeZoneInfo.ConvertTimeFromUtc(start, Istanbul);
            Assert.Equal((DayOfWeek.Tuesday, 9, 1), (local.DayOfWeek, local.Month, local.Day));
        }

        // 3) Aylık rapor: 1 Eylül 00:50 TR'de tamamlanan ders Eylül'e yazılır, Ağustos'a değil.
        await PublishToReportingAsync(factory, new IntegrationEvent(
            Guid.NewGuid(),
            Utc(2026, 8, 31, 21, 50),
            "LessonSessionCompletedDomainEvent",
            "LessonSessions",
            JsonSerializer.Serialize(new
            {
                lessonSessionId = Guid.NewGuid(),
                lessonScheduleId = seriesId,
                teacherUserId,
                studentId,
                completedOnUtc = Utc(2026, 8, 31, 21, 50),
            }, IntegrationEventSerialization.Options)));

        using (var current = await GetJsonAsync(teacher, $"/api/reporting/teachers/{teacherUserId}/monthly"))
        {
            Assert.Equal((2026, 9), (current.RootElement.GetProperty("year").GetInt32(), current.RootElement.GetProperty("month").GetInt32()));
            Assert.Equal(1, current.RootElement.GetProperty("completedLessonCount").GetInt32());
        }

        using (var august = await GetJsonAsync(teacher, $"/api/reporting/teachers/{teacherUserId}/monthly?year=2026&month=8"))
        {
            Assert.Equal(0, august.RootElement.GetProperty("completedLessonCount").GetInt32());
        }
    }

    /// <summary>Olayı yalnız Reporting'in handler'larına verir (genel bus değil) — <c>RealProjectionRebuildTests</c> deseni.</summary>
    private static async Task PublishToReportingAsync(WebApplicationFactory<Program> factory, IntegrationEvent integrationEvent)
    {
        var reportingAssembly = typeof(ReportingDbContext).Assembly;
        await using var scope = factory.Services.CreateAsyncScope();
        var handlers = scope.ServiceProvider
            .GetServices<IIntegrationEventHandler>()
            .Where(handler => handler.GetType().Assembly == reportingAssembly && handler.CanHandle(integrationEvent))
            .ToArray();
        Assert.NotEmpty(handlers);

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(integrationEvent, CancellationToken.None);
        }
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute)
        => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static string Iso(DateTime utc) => Uri.EscapeDataString(utc.ToString("O"));

    private static HttpClient NewClient(WebApplicationFactory<Program> factory, string forwardedFor)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return client;
    }

    private static Task<HttpResponseMessage> CreateLessonAsync(
        HttpClient teacher, Guid teacherUserId, Guid studentId, DateTime startAtUtc, string? recurrenceRule)
        => teacher.PostAsJsonAsync("/api/scheduling/lessons", new
        {
            teacherUserId,
            studentId,
            subject = "Matematik",
            lessonFormat = 2,
            startAtUtc,
            endAtUtc = startAtUtc.AddHours(1),
            timeZone = "Europe/Istanbul",
            recurrenceRule,
            reminderOffsetMinutes = 60,
            locationLabel = "Zoom",
            notes = (string?)null,
        });

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {body}");
        return JsonDocument.Parse(body);
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, int role, string label)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"gece-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = label,
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

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("id").GetGuid();
    }
}
