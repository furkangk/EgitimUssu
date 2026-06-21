using Microsoft.AspNetCore.Mvc.Testing;

namespace EgitimUssu.Tests.Integration;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Live_Health_Endpoint_Should_Return_Success()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Ready_Health_Endpoint_Should_Return_Success()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Meta_Version_Endpoint_Should_List_Modules()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/meta/version");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("Identity", payload, StringComparison.Ordinal);
        Assert.Contains("Teachers", payload, StringComparison.Ordinal);
    }
}
