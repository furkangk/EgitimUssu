using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P15 Task 2 / N-08, N-21: Öğrenciye kayıt yazan uçlar, gerçek Postgres + gerçek DI kaydı + gerçek HTTP
/// hata eşlemesiyle öğretmen–öğrenci bağını ister.
/// <para>
/// Birim testleri authorizer'ı doğrudan kurar; <c>ITeacherStudentAccess</c>'in DI'da kayıtlı olduğunu ve
/// <c>lesson_sessions.schedule_not_owned</c>'ın 400'e değil 403'e eşlendiğini yalnız bu test görür.
/// Tam çapraz-kiracı paketi (okuma uçları, veli, mesaj) P15 Task 9'dadır.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealWriteLinkAuthorizationTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Unlinked_And_Archived_Teachers_Cannot_Write_To_A_Student()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacherA = factory.CreateClient();
        teacherA.DefaultRequestHeaders.Add("X-Forwarded-For", "10.152.0.1");
        var teacherB = factory.CreateClient();
        teacherB.DefaultRequestHeaders.Add("X-Forwarded-For", "10.152.0.2");

        var teacherAId = await RegisterTeacherAsync(teacherA, "OgretmenA");
        var teacherBId = await RegisterTeacherAsync(teacherB, "OgretmenB");
        var studentOfA = await CreateStudentAsync(teacherA, teacherAId, "Ada Sahin");
        var studentOfB = await CreateStudentAsync(teacherB, teacherBId, "Ege Aslan");

        // Bağlı öğretmen yazabilir.
        var lessonOfA = await CreateLessonAsync(teacherA, teacherAId, studentOfA, dayOffset: 1);
        Assert.Equal(HttpStatusCode.OK, lessonOfA.StatusCode);
        var lessonOfAId = (await ReadJsonAsync(lessonOfA)).GetProperty("id").GetGuid();

        // N-08: bağsız öğretmen B, A'nın öğrencisine kendi adıyla ders / borç kaydı yazamaz.
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateLessonAsync(teacherB, teacherBId, studentOfA, dayOffset: 2)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreatePaymentAsync(teacherB, teacherBId, studentOfA)).StatusCode);

        // N-21: bağsız öğretmen B, A'nın öğrencisi için veli daveti üretemez.
        var invite = await teacherB.PostAsJsonAsync(
            $"/api/students/profiles/{studentOfA}/parent-invite",
            new { teacherUserId = teacherBId, childDisplayName = "Ada" });
        Assert.Equal(HttpStatusCode.Forbidden, invite.StatusCode);

        // N-08 ikinci yarı: B kendi öğrencisinin manuel oturumuna A'nın planlı dersini bağlayamaz.
        var foreignSession = await teacherB.PostAsJsonAsync("/api/lesson-sessions", new
        {
            lessonScheduleId = lessonOfAId,
            teacherUserId = teacherBId,
            studentId = studentOfB,
            subject = "Matematik",
            plannedStartAtUtc = DateTime.UtcNow.AddDays(1),
            topicTitle = "Problemler"
        });
        Assert.Equal(HttpStatusCode.Forbidden, foreignSession.StatusCode);
        Assert.Equal("lesson_sessions.schedule_not_owned", (await ReadJsonAsync(foreignSession)).GetProperty("code").GetString());

        // Bağı arşivleyen öğretmen A artık yazamaz...
        var archive = await teacherA.PostAsync($"/api/students/teachers/{teacherAId}/students/{studentOfA}/archive", content: null);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateLessonAsync(teacherA, teacherAId, studentOfA, dayOffset: 3)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreatePaymentAsync(teacherA, teacherAId, studentOfA)).StatusCode);

        // ...ama eski kaydını temizleyebilir (iptal kapsam sormaz).
        var cancel = await teacherA.PostAsJsonAsync($"/api/scheduling/lessons/{lessonOfAId}/cancel", new
        {
            reason = 1,
            isChargeable = false,
            cancellationNote = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
    }

    private static Task<HttpResponseMessage> CreateLessonAsync(HttpClient client, Guid teacherUserId, Guid studentId, int dayOffset)
    {
        var startAtUtc = DateTime.UtcNow.Date.AddDays(dayOffset).AddHours(15);
        return client.PostAsJsonAsync("/api/scheduling/lessons", new
        {
            teacherUserId,
            studentId,
            subject = "Matematik",
            lessonFormat = 2,
            startAtUtc,
            endAtUtc = startAtUtc.AddHours(1),
            timeZone = "Europe/Istanbul",
            recurrenceRule = (string?)null,
            reminderOffsetMinutes = 60,
            locationLabel = "Zoom",
            notes = (string?)null
        });
    }

    private static Task<HttpResponseMessage> CreatePaymentAsync(HttpClient client, Guid teacherUserId, Guid studentId)
        => client.PostAsJsonAsync("/api/payments/records", new
        {
            teacherUserId,
            studentId,
            relatedLessonSessionId = (Guid?)null,
            itemType = 1,
            description = "Ders ucreti",
            currency = "TRY",
            expectedAmount = 1000,
            collectedAmount = 0,
            dueDateUtc = DateTime.UtcNow.AddDays(3),
            collectedOnUtc = (DateTime?)null,
            status = 1,
            billingPeriodStartUtc = (DateTime?)null,
            billingPeriodEndUtc = (DateTime?)null,
            notes = (string?)null
        });

    private static async Task<Guid> RegisterTeacherAsync(HttpClient client, string label)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"bag-{Guid.NewGuid():N}@test.com",
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

    private static async Task<Guid> CreateStudentAsync(HttpClient teacher, Guid teacherUserId, string fullName)
    {
        var response = await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            userId = (Guid?)null,
            createdByTeacherUserId = teacherUserId,
            parentUserId = (Guid?)null,
            fullName,
            gradeLevel = "8. Sınıf",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            goalSummary = (string?)null,
            levelNotes = (string?)null,
            origin = 1,   // StudentOrigin.TeacherManaged
            subjects = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
