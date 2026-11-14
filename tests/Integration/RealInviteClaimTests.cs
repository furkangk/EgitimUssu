using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P15 Task 1 / N-20: Davet kodu tahmini gerçek Postgres + Redis üzerinde durdurulur.
/// <para>
/// Eski kod 6 haneli, süresiz ve rol kontrolsüzdü; yalnız IP başına genel hız sınırı vardı ve
/// tahmin eden kişi isabet ettiğinde doğrudan onaylı birincil veli oluyordu. Yeni davranış:
/// kullanıcı başına 10 başarısız denemeden sonra o saat içinde <b>doğru kod bile</b> 429 döner;
/// kod 8 karakterli güvenli alfabedendir ve küçük harfle girilse de kabul edilir.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealInviteClaimTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Parent_Claim_Is_Locked_After_Ten_Wrong_Codes_Even_For_The_Right_Code()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", "10.151.0.1");
        var guesser = factory.CreateClient();
        guesser.DefaultRequestHeaders.Add("X-Forwarded-For", "10.151.0.2");
        var honestParent = factory.CreateClient();
        honestParent.DefaultRequestHeaders.Add("X-Forwarded-For", "10.151.0.3");

        var teacherUserId = await RegisterAsync(teacher, role: 2, label: "Ogretmen");
        await RegisterAsync(guesser, role: 4, label: "Tahminci");
        await RegisterAsync(honestParent, role: 4, label: "Veli");
        var studentId = await CreateStudentAsync(teacher, teacherUserId);
        var code = await CreateParentInviteAsync(teacher, studentId, teacherUserId);

        Assert.Equal(8, code.Length);
        Assert.Matches("^[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{8}$", code);

        // 10 yanlış deneme: her biri 404 — kod yok.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var wrong = await ClaimAsync(guesser, WrongCodeFor(code, attempt));
            Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        }

        // 11. deneme ve doğru kod: kilitli.
        Assert.Equal((HttpStatusCode)429, (await ClaimAsync(guesser, WrongCodeFor(code, 10))).StatusCode);
        Assert.Equal((HttpStatusCode)429, (await ClaimAsync(guesser, code)).StatusCode);

        // Kilit kullanıcıya özeldir: dürüst veli aynı kodu küçük harfle girip bağlanır.
        var honest = await ClaimAsync(honestParent, code.ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, honest.StatusCode);
        var link = await ReadJsonAsync(honest);
        Assert.Equal("Approved", link.GetProperty("status").GetString());

        // Kod tek kullanımlık: ikinci kullanım 404.
        Assert.Equal(HttpStatusCode.NotFound, (await ClaimAsync(honestParent, code)).StatusCode);
    }

    [SkippableFact]
    public async Task Teacher_Cannot_Claim_A_Parent_Invite()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", "10.151.1.1");

        var teacherUserId = await RegisterAsync(teacher, role: 2, label: "Ogretmen");
        var studentId = await CreateStudentAsync(teacher, teacherUserId);
        var code = await CreateParentInviteAsync(teacher, studentId, teacherUserId);

        var claimed = await ClaimAsync(teacher, code);

        Assert.Equal(HttpStatusCode.Forbidden, claimed.StatusCode);
    }

    private static string WrongCodeFor(string code, int attempt)
    {
        // Doğru koddan farklı, aynı alfabeden deterministik kodlar.
        var candidate = $"WRONG{attempt:D3}".Replace('0', '2').Replace('O', 'Q');
        return candidate == code ? "ZZZZZZZZ" : candidate;
    }

    private static Task<HttpResponseMessage> ClaimAsync(HttpClient parent, string code)
        => parent.PostAsJsonAsync("/api/parents/children/claim-invite", new { inviteCode = code });

    private static async Task<string> CreateParentInviteAsync(HttpClient teacher, Guid studentId, Guid teacherUserId)
    {
        var response = await teacher.PostAsJsonAsync(
            $"/api/students/profiles/{studentId}/parent-invite",
            new { teacherUserId, childDisplayName = "Davet Ogrencisi" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("inviteCode").GetString()!;
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

    private static async Task<Guid> CreateStudentAsync(HttpClient teacher, Guid teacherUserId)
    {
        var response = await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            userId = (Guid?)null,
            createdByTeacherUserId = teacherUserId,
            parentUserId = (Guid?)null,
            fullName = "Davet Ogrencisi",
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
