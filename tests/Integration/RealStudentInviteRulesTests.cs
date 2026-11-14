using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Students.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P15 Task 3 / N-22, N-23: Davet ve profil sahipliği kuralları gerçek HTTP eşlemesiyle. Birim testleri
/// domain kuralını görür; yeni hata kodlarının doğru HTTP durumuna eşlendiğini (varsayılan 400'e düşmediğini,
/// <c>shared.forbidden</c>'a çevrilmediğini — Öğrenilenler #182) yalnız bu test görür.
/// </summary>
[Collection("containers")]
public sealed class RealStudentInviteRulesTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Teacher_Cannot_Bind_A_Profile_To_Someone_Elses_Account()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.154.0.1");
        var victim = NewClient(factory, "10.154.0.2");
        var teacherId = await RegisterAsync(teacher, role: 2, "Kural-Ogretmen");
        var victimId = await RegisterAsync(victim, role: 3, "Kural-Ogrenci");

        // N-23: öğretmen yeni profili gövdeye yazdığı bir öğrenci hesabına bağlayamaz.
        var hijack = await CreateProfileAsync(teacher, userId: victimId, createdByTeacherUserId: teacherId, parentUserId: null, origin: 1);
        Assert.Equal(HttpStatusCode.BadRequest, hijack.StatusCode);
        Assert.Equal("students.field_not_allowed", await ReadCodeAsync(hijack));

        var parentField = await CreateProfileAsync(teacher, userId: null, createdByTeacherUserId: teacherId, parentUserId: Guid.NewGuid(), origin: 1);
        Assert.Equal(HttpStatusCode.BadRequest, parentField.StatusCode);
        Assert.Equal("students.field_not_allowed", await ReadCodeAsync(parentField));

        // Kurban kendi profilini hâlâ açabilir (eskiden 409 user_profile_exists alırdı).
        var own = await CreateProfileAsync(victim, userId: victimId, createdByTeacherUserId: null, parentUserId: null, origin: 2);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [SkippableFact]
    public async Task Invite_Responses_Require_The_Code_And_The_Right_State()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.154.1.1");
        var student = NewClient(factory, "10.154.1.2");
        var stranger = NewClient(factory, "10.154.1.3");
        var teacherId = await RegisterAsync(teacher, role: 2, "Davet-Ogretmen");
        var studentUserId = await RegisterAsync(student, role: 3, "Davet-Ogrenci");
        await RegisterAsync(stranger, role: 3, "Davet-Yabanci");

        // Açık davet (hedefsiz): link kimliği sızsa bile kod olmadan yanıtlanamaz.
        var openProfile = await ReadIdAsync(await CreateProfileAsync(teacher, null, teacherId, null, origin: 1));
        Assert.Equal(HttpStatusCode.NoContent, (await InviteAsync(teacher, teacherId, openProfile, target: null)).StatusCode);
        var (openLinkId, openCode) = await ReadLinkAsync(factory, teacherId, openProfile);

        var blindAccept = await stranger.PostAsJsonAsync($"/api/students/links/{openLinkId}/accept", new { inviteCode = "ZZZZ9999" });
        Assert.Equal(HttpStatusCode.BadRequest, blindAccept.StatusCode);
        Assert.Equal("students.invite_code_mismatch", await ReadCodeAsync(blindAccept));
        var blindReject = await stranger.PostAsJsonAsync($"/api/students/links/{openLinkId}/reject", new { inviteCode = "ZZZZ9999" });
        Assert.Equal(HttpStatusCode.BadRequest, blindReject.StatusCode);
        Assert.Equal("students.invite_code_mismatch", await ReadCodeAsync(blindReject));

        // Doğru kodla kabul: profil kabul edenin hesabına bağlanır.
        Assert.Equal(HttpStatusCode.NoContent,
            (await student.PostAsJsonAsync($"/api/students/links/{openLinkId}/accept", new { inviteCode = openCode })).StatusCode);

        // Kurulmuş bağ: ikinci davet 409, kodu bilen bile reddedemez (409).
        var reinvite = await InviteAsync(teacher, teacherId, openProfile, target: Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, reinvite.StatusCode);
        Assert.Equal("students.link_already_active", await ReadCodeAsync(reinvite));
        var lateReject = await student.PostAsJsonAsync($"/api/students/links/{openLinkId}/reject", new { inviteCode = openCode });
        Assert.Equal(HttpStatusCode.Conflict, lateReject.StatusCode);
        Assert.Equal("students.invalid_link_transition", await ReadCodeAsync(lateReject));

        // Hedefli davet: kodu bilen başka öğrenci kabul edemez (403, kod korunur).
        var targetedProfile = await ReadIdAsync(await CreateProfileAsync(teacher, null, teacherId, null, origin: 1));
        Assert.Equal(HttpStatusCode.NoContent, (await InviteAsync(teacher, teacherId, targetedProfile, target: Guid.NewGuid())).StatusCode);
        var (targetedLinkId, targetedCode) = await ReadLinkAsync(factory, teacherId, targetedProfile);
        var notForMe = await stranger.PostAsJsonAsync($"/api/students/links/{targetedLinkId}/accept", new { inviteCode = targetedCode });
        Assert.Equal(HttpStatusCode.Forbidden, notForMe.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var students = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
        var accepted = await students.StudentProfiles.AsNoTracking().SingleAsync(x => x.Id == openProfile);
        Assert.Equal(studentUserId, accepted.UserId);
        Assert.Equal(TeacherStudentLinkStatus.Linked, (await students.TeacherStudentLinks.AsNoTracking().SingleAsync(x => x.Id == openLinkId)).Status);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, (await students.TeacherStudentLinks.AsNoTracking().SingleAsync(x => x.Id == targetedLinkId)).Status);
        Assert.Null((await students.StudentProfiles.AsNoTracking().SingleAsync(x => x.Id == targetedProfile)).UserId);
    }

    private static HttpClient NewClient(WebApplicationFactory<Program> factory, string forwardedFor)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return client;
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, int role, string label)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"davet-{Guid.NewGuid():N}@test.com",
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

    private static Task<HttpResponseMessage> CreateProfileAsync(
        HttpClient client, Guid? userId, Guid? createdByTeacherUserId, Guid? parentUserId, int origin)
        => client.PostAsJsonAsync("/api/students/profiles", new
        {
            userId,
            createdByTeacherUserId,
            parentUserId,
            fullName = "Kural Ogrencisi",
            gradeLevel = "8. Sınıf",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            goalSummary = (string?)null,
            levelNotes = (string?)null,
            origin,
            subjects = Array.Empty<object>(),
        });

    private static Task<HttpResponseMessage> InviteAsync(HttpClient teacher, Guid teacherId, Guid studentId, Guid? target)
        => teacher.PostAsJsonAsync($"/api/students/teachers/{teacherId}/students/{studentId}/invite", new { targetUserId = target });

    /// <summary>Link kimliği ve davet kodu bugün hiçbir API yanıtında yok (Öğrenilenler #184) — salt-okunur DB.</summary>
    private static async Task<(Guid LinkId, string Code)> ReadLinkAsync(WebApplicationFactory<Program> factory, Guid teacherId, Guid studentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var link = await scope.ServiceProvider.GetRequiredService<StudentsDbContext>().TeacherStudentLinks.AsNoTracking()
            .SingleAsync(x => x.TeacherUserId == teacherId && x.StudentId == studentId);
        return (link.Id, link.InviteCode!);
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
