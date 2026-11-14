using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Modules.Teachers.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P08 Task 3 / M14-1..M14-3, M14-5: raporlama analitiği <b>gerçek Postgres'te</b>.
/// <para>
/// Burada kanıtlananlar birim testiyle görülemez: bileşik anahtarlı projeksiyon tablolarının gerçekten
/// yazılabilmesi, dönem aralığı filtresinin (<c>Year &gt; x || (Year == x &amp;&amp; Month &gt;= y)</c>) SQL'e
/// çevrilebilmesi ve uçların yetki davranışı (Öğrenilenler #81/#83).
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealReportingAnalyticsTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Teacher_Monthly_Endpoint_Should_Read_Projection_And_Deny_Other_Teachers()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.32.0.1");

        var teacherUserId = await RegisterAsync(client, role: 2);
        var studentId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
            var projection = new TeacherMonthlyProjection(teacherUserId, 2026, 9);
            projection.TrackCompletedLesson(Guid.NewGuid(), studentId);
            projection.TrackCompletedLesson(Guid.NewGuid(), studentId);
            projection.TrackCancelledLesson(Guid.NewGuid());
            projection.TrackExpectedPayment(Guid.NewGuid(), 750m);
            projection.AddCollected(250m);
            projection.MarkApplied(Guid.NewGuid(), DateTime.UtcNow);
            dbContext.TeacherMonthlyProjections.Add(projection);
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/reporting/teachers/{teacherUserId}/monthly?year=2026&month=9");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, payload.RootElement.GetProperty("completedLessonCount").GetInt32());
        Assert.Equal(1, payload.RootElement.GetProperty("cancelledLessonCount").GetInt32());
        Assert.Equal(1, payload.RootElement.GetProperty("activeStudentCount").GetInt32());
        Assert.Equal(750m, payload.RootElement.GetProperty("expectedAmount").GetDecimal());
        Assert.Equal(250m, payload.RootElement.GetProperty("collectedAmount").GetDecimal());

        // Aktivite olmayan ay 404 değil, sıfırlı meşru bir cevaptır.
        var emptyMonth = await client.GetAsync($"/api/reporting/teachers/{teacherUserId}/monthly?year=2026&month=1");
        Assert.Equal(HttpStatusCode.OK, emptyMonth.StatusCode);
        using var emptyPayload = JsonDocument.Parse(await emptyMonth.Content.ReadAsStringAsync());
        Assert.Equal(0, emptyPayload.RootElement.GetProperty("completedLessonCount").GetInt32());

        // Başka bir öğretmenin rotası: varsayılan-deny.
        var forbidden = await client.GetAsync($"/api/reporting/teachers/{Guid.NewGuid()}/monthly?year=2026&month=9");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [SkippableFact]
    public async Task Period_Range_Filters_Should_Translate_To_Sql()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var studentId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

            foreach (var (year, week, minutes) in new[] { (2025, 52, 30), (2026, 1, 60), (2026, 10, 90) })
            {
                var study = new StudentStudyProjection(studentId, year, week);
                study.TrackSession(Guid.NewGuid(), minutes);
                study.MarkApplied(Guid.NewGuid(), DateTime.UtcNow);
                dbContext.StudentStudyProjections.Add(study);
            }

            foreach (var (subject, month, score) in new[] { ("Matematik", 8, 40m), ("Matematik", 9, 70m), ("Fizik", 9, 50m) })
            {
                var performance = new StudentPerformanceProjection(studentId, subject, 2026, month);
                performance.TrackTopicMastery("Genel", score);
                performance.MarkApplied(Guid.NewGuid(), DateTime.UtcNow);
                dbContext.StudentPerformanceProjections.Add(performance);
            }

            await dbContext.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IReportingProjectionReader>();

            // Yıl sınırını aşan hafta filtresi: 2025/52 elenir, 2026/1 ve 2026/10 kalır.
            var weeks = await reader.ListStudentStudyAsync(
                studentId, (2026, 1), (2026, 10), CancellationToken.None);
            Assert.Equal(2, weeks.Count);
            Assert.Equal(150, weeks.Sum(item => item.StudyMinutes));   // 60 + 90; 2025/52'deki 30 elendi

            // Ders filtresi + ay aralığı.
            var performance = await reader.ListStudentPerformanceAsync(
                studentId, "Matematik", (2026, 9), null, CancellationToken.None);
            var single = Assert.Single(performance);
            Assert.Equal(9, single.Month);
            Assert.Equal(70, single.MasteryScore);
        }
    }

    [SkippableFact]
    public async Task Free_Slots_Endpoint_Should_Return_Availability_Minus_Lessons()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.32.0.2");

        var teacherUserId = await RegisterAsync(client, role: 2);

        // Uygunluk: Pazartesi 09:00–12:00 TR (= 06:00–09:00Z; şablon yerel saattir — P16 T5 / N-43). Profil doğrudan seed edilir — bu testin konusu
        // uygunluk sözleşmesinin SQL'e çevrilmesi ve ucun hesabı, profil oluşturma sözleşmesi değil.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TeachersDbContext>();
            var profileId = Guid.NewGuid();
            var profile = new TeacherProfile(
                profileId, teacherUserId, "Rapor Öğretmen", "Matematik", "İstanbul", "Kadıköy",
                null, null, TeacherLessonFormat.Online, 5, "Lisans", 500m, "TRY", false, null, DateTime.UtcNow);
            profile.AvailabilitySlots.Add(new TeacherAvailabilitySlot(
                Guid.NewGuid(), profileId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), true, false));
            dbContext.TeacherProfiles.Add(profile);
            await dbContext.SaveChangesAsync();
        }

        // 2026-09-07 Pazartesi.
        var response = await client.GetAsync(
            $"/api/reporting/teachers/{teacherUserId}/free-slots?from=2026-09-07T00:00:00Z&to=2026-09-08T00:00:00Z");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var slots = payload.RootElement.EnumerateArray().ToArray();
        Assert.Single(slots);
        Assert.Equal(180, slots[0].GetProperty("durationMinutes").GetInt32());
        Assert.Equal(new DateTime(2026, 9, 7, 6, 0, 0, DateTimeKind.Utc), slots[0].GetProperty("startAtUtc").GetDateTime().ToUniversalTime());
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, int role)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"rapor-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = "Rapor",
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
}
