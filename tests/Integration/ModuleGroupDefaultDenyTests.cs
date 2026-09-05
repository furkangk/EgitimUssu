using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// A-13: Modül uç grupları <b>varsayılan-deny</b>'dir.
/// <para>
/// Eskiden <c>CreateModuleGroup</c> korumasız bir grup döndürüyor, her modülün
/// <c>RequireAuthorization</c>'ı kendisi çağırması gerekiyordu — koruma bir satırı hatırlamaya
/// bağlıydı. Bu testler kuralı iki yönden de sabitler: hiç <c>RequireAuthorization</c> yazmamış
/// bir modül kapalı olmalı, anonim çalışması gereken uçlar ise açık kalmalı.
/// </para>
/// </summary>
public sealed class ModuleGroupDefaultDenyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ModuleGroupDefaultDenyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    /// <summary>
    /// <c>Matching</c> ve <c>Reviews</c> iskelet modülleri hiçbir zaman
    /// <c>RequireAuthorization</c> çağırmadı; gerçek uçlar kazandıklarında korumasız açılmamalılar.
    /// </summary>
    [Theory]
    [InlineData("/api/matching/status")]
    [InlineData("/api/reviews/status")]
    public async Task Module_That_Never_Asked_For_Authorization_Should_Still_Be_Closed(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Giriş öncesi çalışması gereken uçlar açıkça anonim kalır.</summary>
    [Fact]
    public async Task Anonymous_Endpoints_Stay_Reachable_Without_A_Token()
    {
        using var client = _factory.CreateClient();

        var plans = await client.GetAsync("/api/membership/plans");
        Assert.NotEqual(HttpStatusCode.Unauthorized, plans.StatusCode);

        // Gövdesiz POST 400 döner; burada önemli olan 401 OLMAMASI — uç anonim erişilebilir.
        var login = await client.PostAsync("/api/identity/login", content: null);
        Assert.NotEqual(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
