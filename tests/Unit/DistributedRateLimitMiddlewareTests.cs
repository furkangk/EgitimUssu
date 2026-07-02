using System.Net;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Tests.Unit;

public sealed class DistributedRateLimitMiddlewareTests
{
    [Fact]
    public async Task Denied_Auth_Request_Returns_429_And_Skips_Next()
    {
        var limiter = new FakeRateLimiter(allow: false);
        var nextCalled = false;
        var middleware = Build(limiter, _ => { nextCalled = true; return Task.CompletedTask; });
        var context = MakeContext("/api/identity/login");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.StartsWith("auth:", limiter.LastKey);
    }

    [Fact]
    public async Task Allowed_Business_Request_Calls_Next_With_Default_Policy()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var nextCalled = false;
        var middleware = Build(limiter, _ => { nextCalled = true; return Task.CompletedTask; });
        var context = MakeContext("/api/payments/records");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.StartsWith("default:", limiter.LastKey);
    }

    [Fact]
    public async Task NonApi_Path_Bypasses_Limiter()
    {
        var limiter = new FakeRateLimiter(allow: false);
        var nextCalled = false;
        var middleware = Build(limiter, _ => { nextCalled = true; return Task.CompletedTask; });
        var context = MakeContext("/health/ready");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled); // fail-open by design: sağlık/altyapı uçları limitlenmez
        Assert.Equal(0, limiter.Calls);
    }

    [Fact]
    public async Task Partition_Key_Uses_Forwarded_Client_Ip()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);
        var context = MakeContext("/api/identity/login", forwardedFor: "9.9.9.9, 10.0.0.1");

        await middleware.InvokeAsync(context);

        Assert.Equal("auth:9.9.9.9", limiter.LastKey);
    }

    private static DistributedRateLimitMiddleware Build(IRateLimiter limiter, RequestDelegate next)
        => new(next, limiter, Options.Create(new RateLimitOptions()));

    private static DefaultHttpContext MakeContext(string path, string? forwardedFor = null, string remoteIp = "1.2.3.4")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        if (forwardedFor is not null)
        {
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        }

        return context;
    }

    private sealed class FakeRateLimiter(bool allow) : IRateLimiter
    {
        public int Calls { get; private set; }

        public string? LastKey { get; private set; }

        public Task<bool> TryAcquireAsync(string partitionKey, int permitLimit, TimeSpan window, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastKey = partitionKey;
            return Task.FromResult(allow);
        }
    }
}
