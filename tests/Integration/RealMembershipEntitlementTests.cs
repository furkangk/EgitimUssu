using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Membership.Domain;
using EgitimUssu.Modules.Membership.Infrastructure;
using EgitimUssu.Shared.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P09 Task 7 / K-09: Üyelik zincirinin <b>gerçek Postgres'te</b> uçtan uca doğrulaması —
/// Free öğretmen 6. öğrenciyi ekleyemez → deneme başlar → ekleyebilir → deneme bitince yine kilitlenir.
/// <para>
/// Bu senaryo plan metninde elle koşulacak bir adım olarak yazılmıştı; P06 Task 10'un dersi gereği
/// (InMemory'nin göremediği hatalar ancak gerçek Postgres'te çıkıyor, elle koşum tekrarlanabilir değil)
/// otomatik regresyon testine çevrildi. Burada kanıtlanan şey birim testiyle görülemez: sınırın
/// <b>Membership şemasındaki abonelik kaydından</b> okunup Students modülünün ucuna kadar taşınması,
/// yani iki ayrı şemanın tek bir istekte doğru bağlanması.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealMembershipEntitlementTests(ContainerFixture fixture)
{
    private const string TrialPlanCode = "teacher-premium-monthly-e2e";
    private const int TrialDays = 14;

    [SkippableFact]
    public async Task Free_Teacher_Should_Unlock_Sixth_Student_With_Trial_And_Relock_When_It_Expires()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.32.0.1");

        var teacherUserId = await RegisterTeacherAsync(client);

        // 1) Free öğretmen: sınır entitlement'tan gelir (5) ve beşinciye kadar sorun yok.
        var free = await ReadMembershipAsync(client);
        Assert.Equal(nameof(MembershipTier.Free), free.GetProperty("tier").GetString());
        Assert.Equal(
            EgitimUssu.Modules.Membership.Application.EntitlementPolicy.FreeMaxStudents,
            free.GetProperty("entitlement").GetProperty("maxStudents").GetInt32());
        Assert.True(free.GetProperty("entitlement").GetProperty("canSeeAds").GetBoolean());

        for (var index = 1; index <= 5; index++)
        {
            var created = await CreateStudentAsync(client, teacherUserId, $"Öğrenci {index}");
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        // 2) Altıncı öğrenci kilitli: 409 + paywall'ı tetikleyen hata kodu.
        var blocked = await CreateStudentAsync(client, teacherUserId, "Öğrenci 6");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("students.free_limit_reached", await ErrorCodeAsync(blocked));

        // 3) Deneme başlat (paywall'daki düğmenin sunucu karşılığı).
        await SeedTrialPlanAsync(factory);
        var trial = await client.PostAsJsonAsync(
            "/api/membership/subscriptions/start-trial", new { planCode = TrialPlanCode });
        Assert.Equal(HttpStatusCode.OK, trial.StatusCode);

        var premium = await ReadMembershipAsync(client);
        Assert.Equal(nameof(MembershipTier.Premium), premium.GetProperty("tier").GetString());
        Assert.Equal(nameof(SubscriptionStatus.Trialing), premium.GetProperty("status").GetString());
        Assert.Equal(TrialPlanCode, premium.GetProperty("planCode").GetString());
        Assert.False(premium.GetProperty("entitlement").GetProperty("canSeeAds").GetBoolean());
        Assert.Equal(
            int.MaxValue, premium.GetProperty("entitlement").GetProperty("maxStudents").GetInt32());

        // 4) Aynı öğretmen artık altıncıyı ekleyebiliyor — sınır gerçekten aboneliğe bağlı.
        var unlocked = await CreateStudentAsync(client, teacherUserId, "Öğrenci 6");
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);

        // 5) Deneme dönemi kapanır (dönem sonu geçmişe alınır — takvimi ileri sarmanın veri karşılığı).
        await ExpireSubscriptionPeriodAsync(factory, teacherUserId);

        var expired = await ReadMembershipAsync(client);
        Assert.Equal(nameof(MembershipTier.Free), expired.GetProperty("tier").GetString());
        Assert.Equal(JsonValueKind.Null, expired.GetProperty("effectiveUntilUtc").ValueKind);

        // 6) Kilit geri geldi: deneme bitince yedinci öğrenci yine reddedilir.
        var relocked = await CreateStudentAsync(client, teacherUserId, "Öğrenci 7");
        Assert.Equal(HttpStatusCode.Conflict, relocked.StatusCode);
        Assert.Equal("students.free_limit_reached", await ErrorCodeAsync(relocked));
    }

    [SkippableFact]
    public async Task Trial_Should_Be_Available_Only_Once_Per_User()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.32.0.2");

        var teacherUserId = await RegisterTeacherAsync(client);
        await SeedTrialPlanAsync(factory);

        var first = await client.PostAsJsonAsync(
            "/api/membership/subscriptions/start-trial", new { planCode = TrialPlanCode });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Dönem kapansa bile ikinci deneme verilmez: "ilk ay ücretsiz" kullanıcı başına bir kezdir.
        await ExpireSubscriptionPeriodAsync(factory, teacherUserId);

        var second = await client.PostAsJsonAsync(
            "/api/membership/subscriptions/start-trial", new { planCode = TrialPlanCode });
        Assert.NotEqual(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("membership.trial_already_used", await ErrorCodeAsync(second));
    }

    private static async Task<Guid> RegisterTeacherAsync(HttpClient client)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"uyelik-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = "Üyelik",
            lastName = "Öğretmen",
            phoneNumber = (string?)null,
            roles = new[] { 2 },   // UserRole.Teacher
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var payload = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var userId = payload.RootElement.GetProperty("userId").GetGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", payload.RootElement.GetProperty("accessToken").GetString());
        return userId;
    }

    private static Task<HttpResponseMessage> CreateStudentAsync(
        HttpClient client, Guid teacherUserId, string fullName)
        => client.PostAsJsonAsync("/api/students/profiles", new
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

    private static async Task<JsonElement> ReadMembershipAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/membership/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    /// <summary>Hata gövdesinin kanonik kodu (<c>ApiErrorResponse.Code</c>).</summary>
    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    /// <summary>
    /// Deneme planını doğrudan şemaya yazar: <c>MembershipPlanSeeder</c> yalnız Development'ta koşar
    /// (fiyatlar Q2 ile onaylanmadı), testin kendi planı olması gerekir.
    /// </summary>
    private static async Task SeedTrialPlanAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        if (await dbContext.SubscriptionPlans.AnyAsync(plan => plan.Code == TrialPlanCode))
        {
            return;
        }

        dbContext.SubscriptionPlans.Add(new SubscriptionPlan(
            Guid.NewGuid(),
            TrialPlanCode,
            "Teacher",
            MembershipTier.Premium,
            BillingPeriod.Monthly,
            price: 249m,
            currency: "TRY",
            trialDays: TrialDays,
            isActive: true));
        await dbContext.SaveChangesAsync();
    }

    /// <summary>Aboneliğin dönem sonunu geçmişe alır — denemenin dolmasının veri karşılığı.</summary>
    private static async Task ExpireSubscriptionPeriodAsync(
        WebApplicationFactory<Program> factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        var subscription = await dbContext.UserSubscriptions
            .FirstAsync(row => row.UserId == userId);

        dbContext.Entry(subscription)
            .Property(row => row.CurrentPeriodEndUtc)
            .CurrentValue = DateTime.UtcNow.AddDays(-1);
        await dbContext.SaveChangesAsync();
    }
}
