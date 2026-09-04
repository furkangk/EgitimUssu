using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P06 Task 10 / M11-3: Bildirim merkezi listesi ucu, sorgu parametreleri **verilmediğinde** de
/// çalışmalıdır. `onlyUnread`/`skip`/`take` varsayılansız zorunlu parametrelerdi; eksik gönderen
/// istemci <c>BadHttpRequestException</c> → **500** alıyordu (istemci hatası sunucu hatası gibi görünüyordu).
/// </summary>
[Collection("containers")]
public sealed class RealNotificationCenterEndpointTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Notification_List_Should_Work_Without_Query_Parameters()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.0.1");

        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"bildirim-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = "Bildirim",
            lastName = "Test",
            phoneNumber = (string?)null,
            roles = new[] { 2 },
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var payload = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var userId = payload.RootElement.GetProperty("userId").GetGuid();
        var accessToken = payload.RootElement.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Parametresiz: varsayılanlarla 200 dönmeli (eskiden 500).
        var bare = await client.GetAsync($"/api/notifications/users/{userId}/notifications");
        Assert.Equal(HttpStatusCode.OK, bare.StatusCode);

        // Kısmi parametre de kabul edilmeli.
        var partial = await client.GetAsync($"/api/notifications/users/{userId}/notifications?onlyUnread=true");
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
    }
}
