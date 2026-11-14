using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P15 Task 7 / N-30, A-15: Refresh token rotasyonu gerçek Postgres'te <b>tek kazanana</b> indirgenir.
/// <para>
/// Eski kod oturumu okuyup "iptal edilmemiş" görünce rotasyonu koşulsuz yazıyordu: aynı refresh
/// token'la eşzamanlı iki istek iki geçerli zincir üretiyordu (çalınan token'la yarışan saldırgan
/// tespiti atlatırdı). Sırayla gelen kaybeden ise "yeniden kullanım" sayılıp kullanıcının
/// <b>bütün</b> oturumlarını — kazananın yenisi dahil — öldürüyordu. Mobilin dashboard'da attığı 5
/// paralel isteğin her biri 401 alınca refresh denediği için (N-67) bu, sahada açıklamasız çıkış
/// demekti (S-1).
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealConcurrentRefreshTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Parallel_Refreshes_With_The_Same_Token_Have_Exactly_One_Winner_And_The_Winner_Keeps_Working()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var email = $"yaris-{Guid.NewGuid():N}@test.com";
        var phone = NewClient(factory, "10.171.0.1");
        var tablet = NewClient(factory, "10.171.0.2");
        var phoneSession = await RegisterAsync(phone, email);
        var tabletSession = await LoginAsync(tablet, email, "Tablet");

        // Beş "cihaz içi" istek aynı refresh token'la aynı anda yenilemeye çalışır.
        var racers = Enumerable.Range(0, 5)
            .Select(index => NewClient(factory, $"10.171.1.{index + 1}"))
            .ToArray();
        var responses = await Task.WhenAll(racers.Select(client =>
            client.PostAsJsonAsync("/api/identity/refresh", new { refreshToken = phoneSession.RefreshToken })));

        var winners = responses.Where(response => response.StatusCode == HttpStatusCode.OK).ToArray();
        var winner = Assert.Single(winners);
        foreach (var loser in responses.Except(winners))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, loser.StatusCode);
            Assert.Equal("identity.invalid_refresh_token", await ReadCodeAsync(loser));
        }

        // Kazananın yeni token'ları çalışmaya devam eder — kaybedenler toplu iptal tetiklemedi.
        var rotated = await ReadSessionAsync(winner);
        Authorize(phone, rotated.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/identity/me")).StatusCode);
        var next = await phone.PostAsJsonAsync("/api/identity/refresh", new { refreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);

        // Diğer cihazın oturumuna da dokunulmadı.
        Authorize(tablet, tabletSession.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await tablet.GetAsync("/api/identity/me")).StatusCode);
        var tabletRefresh = await tablet.PostAsJsonAsync("/api/identity/refresh", new { refreshToken = tabletSession.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, tabletRefresh.StatusCode);
    }

    /// <summary>
    /// A-15 korunur: bekleme penceresi dışında (burada 0 sn) rotasyonla eskimiş token geri gelirse
    /// kullanıcının bütün oturumları ve dolaşımdaki erişim token'ları kapanır.
    /// </summary>
    [SkippableFact]
    public async Task Rotated_Token_Presented_After_The_Grace_Window_Revokes_Every_Session()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        using var grace = new EnvironmentVariable("Identity__RefreshReuseGraceSeconds", "0");
        await using var factory = new WebApplicationFactory<Program>();

        var email = $"tekrar-{Guid.NewGuid():N}@test.com";
        var phone = NewClient(factory, "10.172.0.1");
        var tablet = NewClient(factory, "10.172.0.2");
        var thief = NewClient(factory, "10.172.0.3");
        var original = await RegisterAsync(phone, email);
        var tabletSession = await LoginAsync(tablet, email, "Tablet");

        var rotated = await ReadSessionAsync(
            await phone.PostAsJsonAsync("/api/identity/refresh", new { refreshToken = original.RefreshToken }));

        // Eski token ikinci kez sunulur: ele geçirme sinyali.
        var replay = await thief.PostAsJsonAsync("/api/identity/refresh", new { refreshToken = original.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // Kazananın yeni zinciri de, diğer cihaz da kapanmıştır.
        Authorize(phone, rotated.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/identity/me")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await phone.PostAsJsonAsync("/api/identity/refresh", new { refreshToken = rotated.RefreshToken })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await tablet.PostAsJsonAsync("/api/identity/refresh", new { refreshToken = tabletSession.RefreshToken })).StatusCode);
    }

    // --- Yardımcılar ------------------------------------------------------------------

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
            firstName = "Yaris",
            lastName = "Test",
            phoneNumber = (string?)null,
            roles = new[] { 2 },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadSessionAsync(response);
    }

    private static async Task<IssuedSession> LoginAsync(HttpClient client, string email, string deviceName)
    {
        var response = await client.PostAsJsonAsync(
            "/api/identity/login", new { email, password = "Passw0rd!", deviceName });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadSessionAsync(response);
    }

    private static async Task<IssuedSession> ReadSessionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new IssuedSession(
            payload.RootElement.GetProperty("accessToken").GetString()!,
            payload.RootElement.GetProperty("refreshToken").GetString()!);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private sealed record IssuedSession(string AccessToken, string RefreshToken);

    /// <summary>Tek bir ortam değişkenini test süresince kurar, sonunda eski değerine döndürür.</summary>
    private sealed class EnvironmentVariable : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariable(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}
