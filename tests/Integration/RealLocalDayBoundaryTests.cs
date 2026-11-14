using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P16 T5 / N-11, N-50 (K-19) — gerçek Postgres + gerçek HTTP: "bugün", "bu ay" ve "çalışıldı" Türkiye gününe göre.
/// <para>
/// Uygulamanın yerel takvimi sabit bir ana alınır (<see cref="LocalCalendarOverride"/>); <c>IClock</c> gerçek kalır,
/// böylece JWT'nin geçerlilik süresi bozulmaz. Bugünü <c>IClock</c>'tan hesaplayan kod bu testlerde yanlış güne bakar —
/// kırmızı tam olarak bunu gösterir.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealLocalDayBoundaryTests(ContainerFixture fixture)
{
    /// <summary>Çarşamba 18 Kasım 2026 00:45 TR.</summary>
    private static readonly DateTime WednesdayJustAfterMidnight = new(2026, 11, 17, 21, 45, 0, DateTimeKind.Utc);

    [SkippableFact]
    public async Task Dashboard_Today_Is_The_Turkey_Day_And_Overdue_Totals_Are_Per_Currency()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>().WithLocalNow(WednesdayJustAfterMidnight);
        var (teacher, teacherUserId, studentId) = await CreateTeacherWithStudentAsync(factory, "10.182.0.1", "Bugun");

        var today = await ReadIdAsync(await CreateLessonAsync(teacher, teacherUserId, studentId, Utc(2026, 11, 17, 21, 30), 60));   // Çar 00:30 TR
        await ReadIdAsync(await CreateLessonAsync(teacher, teacherUserId, studentId, Utc(2026, 11, 18, 21, 30), 60));               // Per 00:30 TR
        await ReadIdAsync(await CreateLessonAsync(teacher, teacherUserId, studentId, Utc(2026, 11, 17, 20, 0), 60));                // Sal 23:00–00:00 TR

        // Gecikmiş: en erken vadeli TRY (100 + 200) ve USD (50) — farklı para birimleri toplanmaz.
        await CreateOverduePaymentAsync(teacher, teacherUserId, studentId, "TRY", 100m, daysAgo: 10);
        await CreateOverduePaymentAsync(teacher, teacherUserId, studentId, "USD", 50m, daysAgo: 5);
        await CreateOverduePaymentAsync(teacher, teacherUserId, studentId, "TRY", 200m, daysAgo: 3);

        using var summary = await GetJsonAsync(teacher, $"/api/teachers/profiles/{teacherUserId}/dashboard-summary");
        var root = summary.RootElement;

        Assert.Equal(new[] { today }, root.GetProperty("todayLessons").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));

        Assert.True(root.TryGetProperty("overduePaymentTotals", out var totals), "Yanıtta para birimi bazında 'overduePaymentTotals' yok.");
        Assert.Equal(
            new[] { ("TRY", 2, 300m), ("USD", 1, 50m) },
            totals.EnumerateArray().Select(item => (
                item.GetProperty("currency").GetString()!,
                item.GetProperty("count").GetInt32(),
                item.GetProperty("total").GetDecimal())));
        // Eski tekil alanlar (mobil okuyor) ilk satırı taşır — artık yalnız o para biriminin toplamı.
        Assert.Equal("TRY", root.GetProperty("overduePaymentsCurrency").GetString());
        Assert.Equal(300m, root.GetProperty("overduePaymentsTotal").GetDecimal());
        Assert.Equal(3, root.GetProperty("overduePaymentsCount").GetInt32());
    }

    [SkippableFact]
    public async Task Dashboard_Today_Excludes_A_Lesson_That_Started_Before_Local_Midnight()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>().WithLocalNow(WednesdayJustAfterMidnight);
        var (teacher, teacherUserId, studentId) = await CreateTeacherWithStudentAsync(factory, "10.182.1.1", "Gece");

        // Sal 23:30 – Çar 00:30 TR: dün başladı, bugüne taşıyor — "bugünkü ders" değil.
        await ReadIdAsync(await CreateLessonAsync(teacher, teacherUserId, studentId, Utc(2026, 11, 17, 20, 30), 60));

        using var summary = await GetJsonAsync(teacher, $"/api/teachers/profiles/{teacherUserId}/dashboard-summary");
        Assert.Empty(summary.RootElement.GetProperty("todayLessons").EnumerateArray());
    }

    [SkippableFact]
    public async Task Monthly_Report_Defaults_To_The_Turkey_Month()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        // 31 Ağustos 22:00Z = 1 Eylül 01:00 TR.
        await using var factory = new WebApplicationFactory<Program>().WithLocalNow(Utc(2026, 8, 31, 22, 0));
        var (teacher, teacherUserId, _) = await CreateTeacherWithStudentAsync(factory, "10.182.2.1", "Aylik");

        using var monthly = await GetJsonAsync(teacher, $"/api/reporting/teachers/{teacherUserId}/monthly");

        Assert.Equal(2026, monthly.RootElement.GetProperty("year").GetInt32());
        Assert.Equal(9, monthly.RootElement.GetProperty("month").GetInt32());
    }

    [SkippableFact]
    public async Task Plan_Completions_Are_Read_On_Turkey_Days()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>().WithLocalNow(WednesdayJustAfterMidnight);

        var studentId = Guid.NewGuid();
        var tuesdayNight = Guid.NewGuid();      // Sal 01:30 TR = Pzt 22:30Z → Salı
        var wednesdayNight = Guid.NewGuid();    // Çar 00:30 TR = Sal 21:30Z → Çarşamba
        var mondayEvening = Guid.NewGuid();     // Pzt 23:30 TR = Pzt 20:30Z → Pazartesi
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var study = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
            study.StudySessions.AddRange(
                Manual(studentId, tuesdayNight, Utc(2026, 11, 16, 22, 30)),
                Manual(studentId, wednesdayNight, Utc(2026, 11, 17, 21, 30)),
                Manual(studentId, mondayEvening, Utc(2026, 11, 16, 20, 30)));
            await study.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IStudyPlanCompletionReader>();
            var tuesday = await reader.GetCompletionsAsync(studentId, new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 17), CancellationToken.None);

            Assert.Equal(new[] { new PlanCompletion(tuesdayNight, new DateOnly(2026, 11, 17)) }, tuesday);
        }
    }

    private static StudySession Manual(Guid studentId, Guid lessonId, DateTime studiedOnUtc)
        => StudySession.CreateManual(
            Guid.NewGuid(), studentId, "Matematik", null, 30, studiedOnUtc, null, false, false, studiedOnUtc, lessonId);

    private static DateTime Utc(int year, int month, int day, int hour, int minute)
        => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static Task<HttpResponseMessage> CreateLessonAsync(
        HttpClient teacher, Guid teacherUserId, Guid studentId, DateTime startAtUtc, int minutes)
        => teacher.PostAsJsonAsync("/api/scheduling/lessons", new
        {
            teacherUserId,
            studentId,
            subject = "Matematik",
            lessonFormat = 2,
            startAtUtc,
            endAtUtc = startAtUtc.AddMinutes(minutes),
            timeZone = "Europe/Istanbul",
            recurrenceRule = (string?)null,
            reminderOffsetMinutes = 60,
            locationLabel = "Zoom",
            notes = (string?)null,
        });

    private static async Task CreateOverduePaymentAsync(
        HttpClient teacher, Guid teacherUserId, Guid studentId, string currency, decimal amount, int daysAgo)
    {
        var response = await teacher.PostAsJsonAsync("/api/payments/records", new
        {
            teacherUserId,
            studentId,
            relatedLessonSessionId = (Guid?)null,
            itemType = 1,
            description = $"Gecikmis {currency}",
            currency,
            expectedAmount = amount,
            collectedAmount = 0m,
            dueDateUtc = DateTime.UtcNow.AddDays(-daysAgo),
            collectedOnUtc = (DateTime?)null,
            status = 1,
            billingPeriodStartUtc = (DateTime?)null,
            billingPeriodEndUtc = (DateTime?)null,
            notes = (string?)null,
        });
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {body}");
        return JsonDocument.Parse(body);
    }

    private static async Task<(HttpClient Teacher, Guid TeacherUserId, Guid StudentId)> CreateTeacherWithStudentAsync(
        WebApplicationFactory<Program> factory, string forwardedFor, string label)
    {
        var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        var register = await teacher.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"yerelgun-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = label,
            lastName = "Test",
            phoneNumber = (string?)null,
            roles = new[] { 2 },
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        Guid teacherUserId;
        using (var payload = JsonDocument.Parse(await register.Content.ReadAsStringAsync()))
        {
            teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", payload.RootElement.GetProperty("accessToken").GetString());
            teacherUserId = payload.RootElement.GetProperty("userId").GetGuid();
        }

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

        return (teacher, teacherUserId, studentId);
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("id").GetGuid();
    }
}

/// <summary>
/// Uygulamanın yerel takvimini (<see cref="ILocalCalendar"/>) sabit bir ana bağlar; <c>IClock</c> gerçek kalır (JWT
/// geçerliliği ve iş kiraları bozulmaz). "Bugün/bu hafta/bu ay" kararlarını takvimden alan kod bu anı görür.
/// P16 T6 gece yarısı senaryosu da aynı dikişi kullanır.
/// </summary>
internal static class LocalCalendarOverride
{
    public static WebApplicationFactory<Program> WithLocalNow(this WebApplicationFactory<Program> factory, DateTime utcNow)
        => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ILocalCalendar>(new LocalCalendar("Europe/Istanbul", new FixedClock(utcNow)))));

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
