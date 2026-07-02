using System.Text;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Unit;

public sealed class IdempotencyMiddlewareTests
{
    [Fact]
    public async Task Get_Request_Bypasses_Idempotency()
    {
        var store = new FakeIdempotencyStore(IdempotencyOutcome.Proceed);
        var nextCalled = false;
        var middleware = new IdempotencyMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, store);
        var context = MakeContext("GET", "/api/payments/records", idempotencyKey: "abc");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(0, store.BeginCalls);
    }

    [Fact]
    public async Task Post_Without_Key_Bypasses_Idempotency()
    {
        var store = new FakeIdempotencyStore(IdempotencyOutcome.Proceed);
        var nextCalled = false;
        var middleware = new IdempotencyMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, store);
        var context = MakeContext("POST", "/api/payments/records", idempotencyKey: null);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(0, store.BeginCalls);
    }

    [Fact]
    public async Task Duplicate_Replays_Stored_Response_Without_Calling_Next()
    {
        var storedBody = Encoding.UTF8.GetBytes("{\"cached\":true}");
        var store = new FakeIdempotencyStore(IdempotencyOutcome.Duplicate,
            stored: new IdempotentResponse(201, "application/json", storedBody));
        var nextCalled = false;
        var middleware = new IdempotencyMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, store);
        var context = MakeContext("POST", "/api/payments/records", idempotencyKey: "abc");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(201, context.Response.StatusCode);
        Assert.Equal(storedBody, ReadBody(context));
    }

    [Fact]
    public async Task InProgress_Returns_409_Without_Calling_Next()
    {
        var store = new FakeIdempotencyStore(IdempotencyOutcome.InProgress);
        var nextCalled = false;
        var middleware = new IdempotencyMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, store);
        var context = MakeContext("POST", "/api/payments/records", idempotencyKey: "abc");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task Proceed_Executes_And_Caches_Successful_Response()
    {
        var store = new FakeIdempotencyStore(IdempotencyOutcome.Proceed);
        var payload = Encoding.UTF8.GetBytes("{\"id\":1}");
        var middleware = new IdempotencyMiddleware(
            async context =>
            {
                context.Response.StatusCode = 201;
                await context.Response.Body.WriteAsync(payload);
            },
            store);
        var httpContext = MakeContext("POST", "/api/payments/records", idempotencyKey: "abc");

        await middleware.InvokeAsync(httpContext);

        Assert.NotNull(store.Completed);
        Assert.Equal(201, store.Completed!.StatusCode);
        Assert.Equal(payload, store.Completed.Body);
        Assert.Equal(payload, ReadBody(httpContext)); // orijinal gövdeye de yazıldı
    }

    [Fact]
    public async Task Proceed_Does_Not_Cache_Error_Response()
    {
        var store = new FakeIdempotencyStore(IdempotencyOutcome.Proceed);
        var middleware = new IdempotencyMiddleware(
            context => { context.Response.StatusCode = 400; return Task.CompletedTask; },
            store);
        var httpContext = MakeContext("POST", "/api/payments/records", idempotencyKey: "abc");

        await middleware.InvokeAsync(httpContext);

        Assert.Null(store.Completed);
    }

    private static DefaultHttpContext MakeContext(string method, string path, string? idempotencyKey)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (idempotencyKey is not null)
        {
            context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        }

        return context;
    }

    private static byte[] ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var memory = new MemoryStream();
        context.Response.Body.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed class FakeIdempotencyStore(IdempotencyOutcome outcome, IdempotentResponse? stored = null) : IIdempotencyStore
    {
        public int BeginCalls { get; private set; }

        public IdempotentResponse? Completed { get; private set; }

        public Task<(IdempotencyOutcome Outcome, IdempotentResponse? StoredResponse)> TryBeginAsync(string key, CancellationToken cancellationToken = default)
        {
            BeginCalls++;
            return Task.FromResult((outcome, stored));
        }

        public Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken = default)
        {
            Completed = response;
            return Task.CompletedTask;
        }
    }
}
