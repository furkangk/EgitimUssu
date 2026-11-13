using System.Security.Claims;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;

namespace EgitimUssu.Tests.Unit;

public sealed class IdempotencyIdentityIsolationTests
{
    [Theory]
    [InlineData("/api/identity/login")]
    [InlineData("/api/identity/register")]
    [InlineData("/api/public/submissions")]
    public async Task Anonymous_Requests_With_Same_Key_Do_Not_Share_Responses(string path)
    {
        var store = new RecordingStore();
        var calls = 0;
        var middleware = new IdempotencyMiddleware(
            context => context.Response.WriteAsync($"private-response-{++calls}"), store);
        var first = MakeContext(path, authenticated: false);
        var second = MakeContext(path, authenticated: false);

        await middleware.InvokeAsync(first);
        await middleware.InvokeAsync(second);

        Assert.Equal("private-response-1", await ReadBodyAsync(first));
        Assert.Equal("private-response-2", await ReadBodyAsync(second));
        Assert.Equal(2, calls);
        Assert.Equal(0, store.BeginCalls);
        Assert.Empty(store.Completed);
    }

    [Theory]
    [InlineData(false, "untrusted-user-claim")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "   ")]
    public async Task Missing_Authenticated_User_Identity_Bypasses_Cache(bool authenticated, string? userId)
    {
        var store = new RecordingStore();
        var calls = 0;
        var middleware = new IdempotencyMiddleware(
            context => context.Response.WriteAsync($"response-{++calls}"), store);

        await middleware.InvokeAsync(MakeContext("/api/payments/records", authenticated, userId));
        await middleware.InvokeAsync(MakeContext("/api/payments/records", authenticated, userId));

        Assert.Equal(2, calls);
        Assert.Equal(0, store.BeginCalls);
        Assert.Empty(store.Completed);
    }

    [Theory]
    [InlineData("/api/identity/login")]
    [InlineData("/api/identity/refresh")]
    [InlineData("/api/identity/logout")]
    [InlineData("/api/identity/sessions/revoke-all")]
    [InlineData("/API/IDENTITY/refresh")]
    public async Task Authenticated_Identity_Request_Revalidates_State_Instead_Of_Replaying_Tokens(string path)
    {
        var store = new RecordingStore();
        var revoked = false;
        var calls = 0;
        var middleware = new IdempotencyMiddleware(async context =>
        {
            calls++;
            context.Response.StatusCode = revoked ? StatusCodes.Status401Unauthorized : StatusCodes.Status200OK;
            await context.Response.WriteAsync(revoked ? "revoked" : "sensitive-token");
        }, store);
        var first = MakeContext(path, authenticated: true, "user-a");
        await middleware.InvokeAsync(first);
        revoked = true;
        var second = MakeContext(path, authenticated: true, "user-a");

        await middleware.InvokeAsync(second);

        Assert.Equal("sensitive-token", await ReadBodyAsync(first));
        Assert.Equal(StatusCodes.Status401Unauthorized, second.Response.StatusCode);
        Assert.Equal("revoked", await ReadBodyAsync(second));
        Assert.Equal(2, calls);
        Assert.Equal(0, store.BeginCalls);
        Assert.Empty(store.Completed);
    }

    [Fact]
    public async Task Authenticated_Mutations_Isolate_Users_And_Replay_Only_Own_Response()
    {
        var store = new RecordingStore();
        var calls = 0;
        var middleware = new IdempotencyMiddleware(context =>
        {
            calls++;
            return context.Response.WriteAsync($"private:{context.User.FindFirstValue(ClaimTypes.NameIdentifier)}");
        }, store);
        var first = MakeContext("/api/payments/records", authenticated: true, "user-a");
        var second = MakeContext("/api/payments/records", authenticated: true, "user-b");
        var repeatedFirst = MakeContext("/api/payments/records", authenticated: true, "user-a");

        await middleware.InvokeAsync(first);
        await middleware.InvokeAsync(second);
        await middleware.InvokeAsync(repeatedFirst);

        Assert.Equal("private:user-a", await ReadBodyAsync(first));
        Assert.Equal("private:user-b", await ReadBodyAsync(second));
        Assert.Equal("private:user-a", await ReadBodyAsync(repeatedFirst));
        Assert.Equal(2, calls);
        Assert.Equal(3, store.BeginCalls);
        Assert.Equal(2, store.Completed.Count);
    }

    private static DefaultHttpContext MakeContext(string path, bool authenticated, string? userId = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        context.Request.Headers["Idempotency-Key"] = "same-key";
        context.Response.Body = new MemoryStream();
        Claim[] claims = userId is null ? [] : [new Claim(ClaimTypes.NameIdentifier, userId)];
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "Test" : null));
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class RecordingStore : IIdempotencyStore
    {
        public int BeginCalls { get; private set; }
        public Dictionary<string, IdempotentResponse> Completed { get; } = [];

        public Task<(IdempotencyOutcome Outcome, IdempotentResponse? StoredResponse)> TryBeginAsync(
            string key, CancellationToken cancellationToken = default)
        {
            BeginCalls++;
            return Task.FromResult(Completed.TryGetValue(key, out var response)
                ? (IdempotencyOutcome.Duplicate, (IdempotentResponse?)response)
                : (IdempotencyOutcome.Proceed, (IdempotentResponse?)null));
        }

        public Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken = default)
        {
            Completed[key] = response;
            return Task.CompletedTask;
        }
    }
}
