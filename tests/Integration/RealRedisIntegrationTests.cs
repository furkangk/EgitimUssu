using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// M14/Y4: Dağıtık rate limiting'in gerçek Redis'e karşı doğrulanması. Aşama 1'de yalnız fake'lerle
/// test edilmişti; burada gerçek Redis sayacı ile "auth" politikası (10/dk) aşımında 429 dönmeli.
/// </summary>
[Collection("containers")]
public sealed class RealRedisIntegrationTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Auth_Endpoint_Returns_429_After_Limit_With_Real_Redis()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        // Bu teste özel IP → rate-limit sayacı diğer testlerden yalıtık.
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.2");

        // "auth" politikası: IP başına 10/dk. Aynı pencerede 11 istek → en az biri 429 olmalı.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
        {
            var response = await client.PostAsJsonAsync("/api/identity/register", new
            {
                email = $"m14-rl-{i}@test.com",
                password = "Passw0rd!",
                firstName = "R",
                lastName = "L",
                phoneNumber = (string?)null,
                roles = new[] { 2 },
            });
            statuses.Add(response.StatusCode);
        }

        // İlk istek limitlenmemeli; pencere dolunca 429 görülmeli (gerçek Redis sayacı çalışıyor, fail-open değil).
        Assert.NotEqual(HttpStatusCode.TooManyRequests, statuses[0]);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
