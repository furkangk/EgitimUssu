using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P13 Task 2 / C-10: API sözleşme sürümü.
/// <para>
/// Kural: istemci hangi sözleşmeye karşı konuştuğunu <b>yanıttan</b> öğrenebilmeli. Mobil istemci
/// mağazadaki eski sürümüyle aylarca yaşadığı için "sunucu sözleşmeyi değiştirdi mi?" sorusu
/// tahmine bırakılamaz; her <c>/api/*</c> yanıtı sözleşme sürümünü taşır.
/// </para>
/// </summary>
public sealed class ApiVersionHeaderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string HeaderName = "X-Api-Version";

    private readonly WebApplicationFactory<Program> _factory;

    public ApiVersionHeaderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    /// <summary>
    /// Başlık ile <c>/api/meta/version</c> gövdesi <b>aynı</b> değeri söylemeli — iki kaynak
    /// ayrışırsa istemci hangisine inanacağını bilemez.
    /// </summary>
    [Fact]
    public async Task Meta_Version_Body_And_Header_Should_Agree()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/meta/version");

        response.EnsureSuccessStatusCode();
        var header = Assert.Single(response.Headers.GetValues(HeaderName));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement.GetProperty("apiVersion").GetString();

        Assert.Equal(body, header);
    }

    /// <summary>
    /// Başlık yalnız başarı yanıtına değil, <b>her</b> yanıta aittir: kimlik doğrulama hatası (401)
    /// ya da bilinmeyen yol (404) da sözleşme sürümünü taşır — istemci hata gördüğünde de hangi
    /// sürüme baktığını bilmelidir.
    /// </summary>
    [Theory]
    [InlineData("/api/meta/version")]                                          // 200 — modül dışı uç
    [InlineData("/api/identity/me")]                                           // 401 — modül grubu
    [InlineData("/api/students/profiles/00000000-0000-0000-0000-000000000000")] // 401 — başka modül grubu
    [InlineData("/api/boyle-bir-uc-yok")]                                      // 404 — eşleşmeyen yol
    public async Task Every_Api_Response_Should_Carry_Version_Header(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.True(
            response.Headers.Contains(HeaderName),
            $"{path} yanıtı {HeaderName} başlığı taşımıyor (durum: {(int)response.StatusCode}).");
    }

    /// <summary>
    /// Sürüm başlığı API sözleşmesine aittir; altyapı probe'ları (<c>/health/*</c>) sözleşmenin
    /// parçası değildir ve sürümlenmez.
    /// </summary>
    [Fact]
    public async Task Health_Endpoints_Should_Not_Be_Versioned()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.False(response.Headers.Contains(HeaderName));
    }
}
