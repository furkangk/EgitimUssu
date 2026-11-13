using EgitimUssu.Shared.Infrastructure.Middleware;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Http;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P13 Task 2 / C-10: Sürüm başlığının hangi değeri seçtiği.
/// <para>
/// İki kaynak var ve sıra önemlidir: ucun kendi <see cref="ApiVersionMetadata"/> etiketi (modül
/// grubundan gelir) ve yol tabanlı varsayılan. Etiket <b>kazanır</b> — <c>/api/v2/...</c> grubu
/// açıldığında o gruptaki uçlar başka hiçbir değişiklik olmadan doğru sürümü bildirir.
/// </para>
/// </summary>
public sealed class ApiVersionHeaderMiddlewareTests
{
    [Fact]
    public async Task Endpoint_Metadata_Should_Win_Over_Path_Default()
    {
        var context = CreateContext("/api/identity/me");
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new ApiVersionMetadata("v2")),
            "v2-endpoint"));

        await InvokeAsync(context);

        Assert.Equal("v2", context.Response.Headers[ApiVersionHeaderMiddleware.HeaderName]);
    }

    /// <summary>Etiketsiz bir <c>/api</c> ucu bile sürümsüz kalmamalı — güvenlik ağı.</summary>
    [Fact]
    public async Task Unlabelled_Api_Path_Should_Fall_Back_To_Current_Version()
    {
        var context = CreateContext("/api/meta/version");

        await InvokeAsync(context);

        Assert.Equal(ApiVersions.Current, context.Response.Headers[ApiVersionHeaderMiddleware.HeaderName]);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/")]
    public async Task Non_Api_Paths_Should_Not_Be_Versioned(string path)
    {
        var context = CreateContext(path);

        await InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey(ApiVersionHeaderMiddleware.HeaderName));
    }

    /// <summary><c>/apixyz</c> "api" ile başlar ama <c>/api</c> segmenti değildir.</summary>
    [Fact]
    public async Task Path_Prefix_Should_Match_Whole_Segment_Only()
    {
        var context = CreateContext("/apiary/tour");

        await InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey(ApiVersionHeaderMiddleware.HeaderName));
    }

    /// <summary>
    /// Başlık, sonraki middleware çağrılmadan <b>önce</b> yazılmalı: yanıt gövdesi akmaya
    /// başladıktan sonra başlık eklenemez, 401/500 gibi erken yanıtlar sürümsüz kalırdı.
    /// </summary>
    [Fact]
    public async Task Header_Should_Be_Written_Before_Next_Middleware_Runs()
    {
        var context = CreateContext("/api/identity/login");
        var headerSeenByNext = string.Empty;

        var middleware = new ApiVersionHeaderMiddleware(ctx =>
        {
            headerSeenByNext = ctx.Response.Headers[ApiVersionHeaderMiddleware.HeaderName].ToString();
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(ApiVersions.Current, headerSeenByNext);
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context;
    }

    private static Task InvokeAsync(HttpContext context)
    {
        return new ApiVersionHeaderMiddleware(_ => Task.CompletedTask).InvokeAsync(context);
    }
}
