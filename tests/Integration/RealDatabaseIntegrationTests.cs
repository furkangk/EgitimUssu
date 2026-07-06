using System.Net;
using System.Net.Http.Json;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// M14: Gerçek Postgres'e karşı — migration'lar uygulanıyor mu ve InMemory'nin doğrulayamadığı
/// gerçek-DB davranışları (unique constraint) zorlanıyor mu?
/// </summary>
[Collection("containers")]
public sealed class RealDatabaseIntegrationTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task App_Boots_On_Real_Postgres_And_Enforces_Unique_Email()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        // Rate-limit partition'ını diğer testlerden ayır (paylaşılan Redis + "unknown" IP çakışmasını önle).
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.0.1");

        var register = new
        {
            email = "m14-unique@test.com",
            password = "Passw0rd!",
            firstName = "M",
            lastName = "14",
            phoneNumber = (string?)null,
            roles = new[] { 2 },
        };

        // İlk kayıt gerçek Postgres'e yazılır (migration'lar uygulandı → tablolar mevcut).
        var first = await client.PostAsJsonAsync("/api/identity/register", register);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Aynı e-posta → NormalizedEmail unique index gerçek DB'de ihlali yakalar → 409.
        var duplicate = await client.PostAsJsonAsync("/api/identity/register", register);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }
}
