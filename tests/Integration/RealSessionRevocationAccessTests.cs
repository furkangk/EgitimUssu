using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Caching;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P15 Task 7 / N-29, N-31, N-32: Oturum kapatıldığında o oturumun <b>erişim token'ı</b> da bir
/// sonraki istekte reddedilir; kullanıcının bilerek kapattığı oturum ele geçirme sayılmaz.
/// <list type="bullet">
/// <item>N-31: cihaz iptali ve "diğerlerini kapat" yalnız refresh oturumunu kapatıyordu; çalınan
/// telefonun erişim token'ı 60 dk daha veri yazabiliyordu.</item>
/// <item>N-29: "diğer oturumları sonlandır" açıkken telefonla giriş → eski tablet refresh deneyince
/// "yeniden kullanım" sayılıyor ve <b>telefonun yeni oturumu da</b> öldürülüyordu.</item>
/// <item>N-32: kullanıcı damgası saniye çözünürlüğündeydi; iptalle aynı saniyede verilmiş token
/// geçiyordu.</item>
/// </list>
/// </summary>
[Collection("containers")]
public sealed class RealSessionRevocationAccessTests(ContainerFixture fixture)
{
    /// <summary>
    /// Tablet önce bir kez yenilenir: <c>sid</c> her rotasyonda değişir, kapatılan şey tabletin
    /// <b>güncel</b> satırıdır ve engellenmesi gereken de onun token'ıdır.
    /// </summary>
    [SkippableFact]
    public async Task Revoked_Device_Session_Access_Token_Is_Rejected_On_The_Next_Request()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var email = $"cihaz-{Guid.NewGuid():N}@test.com";
        var phone = NewClient(factory, "10.173.0.1");
        var tablet = NewClient(factory, "10.173.0.2");
        Authorize(phone, (await RegisterAsync(phone, email)).AccessToken);
        var tabletLogin = await LoginAsync(tablet, email, "Tablet");
        var tabletSession = await RefreshAsync(tablet, tabletLogin.RefreshToken);
        Authorize(tablet, tabletSession.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await tablet.GetAsync("/api/identity/me")).StatusCode);

        var tabletSessionId = await FindOtherSessionIdAsync(phone);
        var revoke = await phone.PostAsync($"/api/identity/sessions/{tabletSessionId}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await tablet.GetAsync("/api/identity/me")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await tablet.PostAsJsonAsync("/api/identity/refresh", new { refreshToken = tabletSession.RefreshToken })).StatusCode);

        // İptal eden cihaz etkilenmez.
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/identity/me")).StatusCode);
    }

    [SkippableFact]
    public async Task Revoke_All_Rejects_Other_Access_Tokens_And_Keeps_The_Current_Device()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var email = $"hepsi-{Guid.NewGuid():N}@test.com";
        var phone = NewClient(factory, "10.174.0.1");
        var tablet = NewClient(factory, "10.174.0.2");
        var laptop = NewClient(factory, "10.174.0.3");
        Authorize(phone, (await RegisterAsync(phone, email)).AccessToken);
        Authorize(tablet, (await LoginAsync(tablet, email, "Tablet")).AccessToken);
        Authorize(laptop, (await LoginAsync(laptop, email, "Laptop")).AccessToken);

        var revokeAll = await phone.PostAsync("/api/identity/sessions/revoke-all", null);
        Assert.Equal(HttpStatusCode.OK, revokeAll.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await tablet.GetAsync("/api/identity/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/identity/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/identity/me")).StatusCode);
    }

    /// <summary>
    /// N-29: Kullanıcı "diğer oturumları sonlandır"ı seçer, telefonla girer. Kapatılan tablet sonra
    /// refresh dener: düz 401 alır, ama telefonun yeni oturumu yaşamaya devam eder.
    /// </summary>
    [SkippableFact]
    public async Task Terminate_Other_Sessions_Login_Survives_The_Old_Device_Refreshing()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var email = $"tekcihaz-{Guid.NewGuid():N}@test.com";
        var tablet = NewClient(factory, "10.175.0.1");
        var phone = NewClient(factory, "10.175.0.2");
        var tabletSession = await RegisterAsync(tablet, email);
        Authorize(tablet, tabletSession.AccessToken);
        await EnableTerminateOtherSessionsAsync(tablet, tabletSession.UserId);

        var phoneSession = await LoginAsync(phone, email, "Telefon");
        Authorize(phone, phoneSession.AccessToken);

        // Tabletin oturumu kapandı: erişim token'ı da (sid) reddedilir, refresh düz 401.
        Assert.Equal(HttpStatusCode.Unauthorized, (await tablet.GetAsync("/api/identity/me")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await tablet.PostAsJsonAsync("/api/identity/refresh", new { refreshToken = tabletSession.RefreshToken })).StatusCode);

        // Telefon yaşıyor: ne erişim token'ı ne refresh zinciri toplu iptale kurban gitti.
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/identity/me")).StatusCode);
        var phoneRefresh = await phone.PostAsJsonAsync("/api/identity/refresh", new { refreshToken = phoneSession.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, phoneRefresh.StatusCode);
    }

    /// <summary>
    /// N-31: Hesap aktif değilse (askıya alma, kapanış) dolaşımdaki erişim token'ı da reddedilir —
    /// durum her istekte (Redis önbellekli) sorulur.
    /// </summary>
    [SkippableFact]
    public async Task Access_Token_Of_A_Suspended_Account_Is_Rejected()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var client = NewClient(factory, "10.176.0.1");
        var session = await RegisterAsync(client, $"askida-{Guid.NewGuid():N}@test.com");
        Authorize(client, session.AccessToken);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.UserAccounts
                .Where(account => account.Id == session.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(account => account.Status, UserAccountStatus.Suspended));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/me")).StatusCode);
    }

    /// <summary>
    /// N-32: Kullanıcı damgası milisaniye çözünürlüğündedir — iptalden 400 ms önce (aynı saniyede)
    /// verilmiş token reddedilir, 200 ms sonra verilen kabul edilir.
    /// </summary>
    [SkippableFact]
    public async Task User_Stamp_Revokes_Tokens_Issued_Earlier_In_The_Same_Second()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var blacklist = factory.Services.GetRequiredService<ITokenBlacklist>();

        var userId = Guid.NewGuid().ToString();
        var second = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var cutoff = second.AddMilliseconds(600);
        await blacklist.RevokeUserTokensIssuedBeforeAsync(userId, cutoff, TimeSpan.FromMinutes(5));

        Assert.True(await blacklist.IsRevokedAsync("jti-once", userId, null, second.AddMilliseconds(200)));
        Assert.False(await blacklist.IsRevokedAsync("jti-sonra", userId, null, second.AddMilliseconds(800)));
    }

    // --- Yardımcılar ------------------------------------------------------------------

    private static async Task EnableTerminateOtherSessionsAsync(HttpClient client, Guid userId)
    {
        var response = await client.PutAsJsonAsync($"/api/settings/users/{userId}", new
        {
            pushNotificationsEnabled = true,
            emailNotificationsEnabled = true,
            upcomingLessonReminderEnabled = true,
            homeworkReminderEnabled = true,
            paymentReminderEnabled = true,
            weeklySummaryEnabled = true,
            privacyLevel = 2,
            sessionTerminationPolicy = 2, // TerminateOtherSessions
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Guid> FindOtherSessionIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/identity/sessions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var other = Assert.Single(
            payload.RootElement.EnumerateArray(),
            session => !session.GetProperty("isCurrent").GetBoolean());
        return other.GetProperty("sessionId").GetGuid();
    }

    private static HttpClient NewClient(WebApplicationFactory<Program> factory, string forwardedFor)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return client;
    }

    private static void Authorize(HttpClient client, string accessToken)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    private static async Task<IssuedSession> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email,
            password = "Passw0rd!",
            firstName = "Oturum",
            lastName = "Test",
            phoneNumber = (string?)null,
            roles = new[] { 2 },
        });
        return await ReadSessionAsync(response);
    }

    private static async Task<IssuedSession> LoginAsync(HttpClient client, string email, string deviceName)
        => await ReadSessionAsync(await client.PostAsJsonAsync(
            "/api/identity/login", new { email, password = "Passw0rd!", deviceName }));

    private static async Task<IssuedSession> RefreshAsync(HttpClient client, string refreshToken)
        => await ReadSessionAsync(await client.PostAsJsonAsync(
            "/api/identity/refresh", new { refreshToken }));

    private static async Task<IssuedSession> ReadSessionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new IssuedSession(
            payload.RootElement.GetProperty("userId").GetGuid(),
            payload.RootElement.GetProperty("accessToken").GetString()!,
            payload.RootElement.GetProperty("refreshToken").GetString()!);
    }

    private sealed record IssuedSession(Guid UserId, string AccessToken, string RefreshToken);
}
