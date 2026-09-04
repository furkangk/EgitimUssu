using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Shared.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P08 Task 4 / M14-4: PDF öğrenci raporu <b>gerçek Postgres + gerçek dosya deposu</b> üzerinde.
/// <para>
/// Birim testiyle görülemeyen üç şey burada kanıtlanır: rapor kaydının gerçekten yazılabilmesi
/// (<c>student_reports</c> tablosu + migration), üretilen PDF'in depoya yazılıp indirme ucundan
/// bayt bayt geri okunabilmesi ve premium kapısının uçta 402'ye eşlenmesi (Öğrenilenler #81).
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealStudentReportTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Premium_Student_Should_Generate_And_Download_Pdf_Report()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.33.0.1");

        var studentUserId = await RegisterAsync(client, role: 3);
        var studentId = await SeedStudentAsync(factory, studentUserId, MembershipTier.Premium);
        await SeedProjectionsAsync(factory, studentId);

        var generate = await client.PostAsJsonAsync(
            $"/api/reporting/students/{studentId}/report",
            new { from = "2026-08-01", to = "2026-08-31", note = "Dönem sonu değerlendirmesi." });
        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);

        using var payload = JsonDocument.Parse(await generate.Content.ReadAsStringAsync());
        var reportId = payload.RootElement.GetProperty("reportId").GetGuid();
        Assert.True(payload.RootElement.GetProperty("sizeBytes").GetInt32() > 1024);

        // Kayıt gerçekten yazıldı mı (migration + bileşik olmayan PK + ValueGeneratedNever)?
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
            var stored = await dbContext.StudentReports.AsNoTracking()
                .SingleAsync(report => report.Id == reportId);
            Assert.Equal(studentId, stored.StudentId);
            Assert.StartsWith($"reporting/{studentId}/", stored.StorageKey, StringComparison.Ordinal);
        }

        var download = await client.GetAsync($"/api/reporting/reports/{reportId}/file");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);

        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1024, $"İndirilen PDF beklenenden küçük: {bytes.Length} bayt");
        Assert.Equal("%PDF-"u8.ToArray(), bytes.Take(5).ToArray());

        // Bağı olmayan üçüncü bir kullanıcı aynı raporu indiremez (varsayılan-deny).
        var stranger = factory.CreateClient();
        stranger.DefaultRequestHeaders.Add("X-Forwarded-For", "10.33.0.2");
        await RegisterAsync(stranger, role: 2);
        var forbidden = await stranger.GetAsync($"/api/reporting/reports/{reportId}/file");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [SkippableFact]
    public async Task Free_Student_Should_Get_Payment_Required()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.33.0.3");

        var studentUserId = await RegisterAsync(client, role: 3);
        var studentId = await SeedStudentAsync(factory, studentUserId, MembershipTier.Free);

        var generate = await client.PostAsJsonAsync(
            $"/api/reporting/students/{studentId}/report", new { note = (string?)null });

        // Kaynak var, yetki var; eksik olan üyelik → 402 (403 değil).
        Assert.Equal(HttpStatusCode.PaymentRequired, generate.StatusCode);
    }

    private static async Task<Guid> SeedStudentAsync(
        WebApplicationFactory<Program> factory, Guid studentUserId, MembershipTier tier)
    {
        var studentId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
        var profile = new StudentProfile(
            studentId, studentUserId, null, null, "Rapor Öğrenci", "11",
            null, null, null, null, StudentOrigin.SelfRegistered, true, DateTime.UtcNow);
        profile.SetMembershipTier(tier, DateTime.UtcNow);
        dbContext.StudentProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
        return studentId;
    }

    private static async Task SeedProjectionsAsync(WebApplicationFactory<Program> factory, Guid studentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        var study = new StudentStudyProjection(studentId, 2026, 32);
        study.TrackSession(Guid.NewGuid(), 90);
        study.MarkApplied(Guid.NewGuid(), DateTime.UtcNow);
        dbContext.StudentStudyProjections.Add(study);

        var performance = new StudentPerformanceProjection(studentId, "Matematik", 2026, 8);
        performance.TrackTopicMastery("Türev", 70m);
        performance.TrackTestNet(Guid.NewGuid(), 0.62m);
        performance.MarkApplied(Guid.NewGuid(), DateTime.UtcNow);
        dbContext.StudentPerformanceProjections.Add(performance);

        var monthly = new TeacherMonthlyProjection(Guid.NewGuid(), 2026, 8);
        monthly.TrackCompletedLesson(Guid.NewGuid(), studentId);
        monthly.TrackCompletedLesson(Guid.NewGuid(), studentId);
        monthly.MarkApplied(Guid.NewGuid(), DateTime.UtcNow);
        dbContext.TeacherMonthlyProjections.Add(monthly);

        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, int role)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"pdf-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = "Pdf",
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
