using System.Security.Claims;
using System.Security.Cryptography;
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

    /// <summary>
    /// A-12: Başarısız bir istek "işleniyor" işaretini serbest bırakmalı. Eskiden bırakılmıyordu:
    /// kullanıcının aynı <c>Idempotency-Key</c> ile yaptığı yeniden deneme, TTL dolana kadar
    /// <c>409</c> alıyordu — koruma tam da tekrar denenmesi gereken durumda işi engelliyordu.
    /// </summary>
    [Fact]
    public async Task Failed_Response_Releases_The_Processing_Marker()
    {
        var store = new FakeIdempotencyStore(IdempotencyOutcome.Proceed);
        var middleware = new IdempotencyMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return Task.CompletedTask;
            },
            store);
        var context = MakeContext("POST", "/api/payments/records", idempotencyKey: "abc");

        await middleware.InvokeAsync(context);

        Assert.Null(store.Completed);
        Assert.Equal(1, store.ReleaseCalls);
    }

    /// <summary>İstisna da başarısızlıktır: anahtar kilitli kalmamalı.</summary>
    [Fact]
    public async Task Thrown_Request_Releases_The_Processing_Marker()
    {
        var store = new FakeIdempotencyStore(IdempotencyOutcome.Proceed);
        var middleware = new IdempotencyMiddleware(
            _ => throw new InvalidOperationException("boom"),
            store);
        var context = MakeContext("POST", "/api/payments/records", idempotencyKey: "abc");

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Equal(1, store.ReleaseCalls);
    }

    /// <summary>Başarılı yanıtta işaret silinmez — üzerine saklı yanıt yazılır.</summary>
    [Fact]
    public async Task Successful_Response_Is_Stored_And_Not_Released()
    {
        var store = new FakeIdempotencyStore(IdempotencyOutcome.Proceed);
        var middleware = new IdempotencyMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status201Created;
                return context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("{\"ok\":true}")).AsTask();
            },
            store);
        var context = MakeContext("POST", "/api/payments/records", idempotencyKey: "abc");

        await middleware.InvokeAsync(context);

        Assert.NotNull(store.Completed);
        Assert.Equal(0, store.ReleaseCalls);
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

    [Fact]
    public async Task Different_Body_Returns_422_And_Does_Not_Release_Original_Key()
    {
        var store = new FakeIdempotencyStore(IdempotencyOutcome.BodyMismatch);
        var calls = 0;
        var middleware = new IdempotencyMiddleware(_ => { calls++; return Task.CompletedTask; }, store);
        var context = MakeContext("POST", "/api/payments/records", "same-key");
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"amount\":200}"));

        await middleware.InvokeAsync(context);

        Assert.Equal(422, context.Response.StatusCode);
        Assert.Contains("shared.idempotency_key_reused", Encoding.UTF8.GetString(ReadBody(context)));
        Assert.Equal(0, calls);
        Assert.Equal(0, store.ReleaseCalls);
        Assert.Null(store.Completed);
    }

    [Theory]
    [InlineData("application/json", "{\"amount\":1500,\"note\":\"Öğrenci\"}")]
    [InlineData("multipart/form-data; boundary=test-boundary", "--test-boundary\r\nContent-Disposition: form-data; name=\"file\"; filename=\"ödev.txt\"\r\n\r\nödev\r\n--test-boundary--\r\n")]
    [InlineData("application/json", "")]
    public async Task Raw_Body_Hash_Is_Passed_To_Store_And_Completed_Response_Without_Consuming_Body(string contentType, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var store = new FakeIdempotencyStore(IdempotencyOutcome.Proceed);
        byte[]? received = null;
        var middleware = new IdempotencyMiddleware(async ctx =>
        {
            using var memory = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(memory);
            received = memory.ToArray();
        }, store);
        var context = MakeContext("POST", "/api/payments/records", "same-key");
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(bytes);

        await middleware.InvokeAsync(context);

        Assert.Equal(bytes, received);
        var expected = Convert.ToHexString(SHA256.HashData(bytes));
        Assert.Equal(expected, store.RequestBodyHash);
        Assert.Equal(expected, store.Completed?.RequestBodyHash);
    }

    [Fact]
    public async Task NonSeekable_Chunked_Upload_Is_Hashed_And_Rewound_After_Disk_Buffering()
    {
        var bytes = new byte[128 * 1024];
        Random.Shared.NextBytes(bytes);
        var store = new FakeIdempotencyStore(IdempotencyOutcome.Proceed);
        var middleware = new IdempotencyMiddleware(async ctx =>
        {
            using var received = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(received);
            Assert.Equal(bytes, received.ToArray());
        }, store);
        var context = MakeContext("POST", "/api/assignments/submissions", "upload");
        using var source = new NonSeekableStream(bytes);
        context.Request.Body = source;
        try
        {
            await middleware.InvokeAsync(context);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), store.Completed?.RequestBodyHash);
        }
        finally
        {
            await context.Request.Body.DisposeAsync();
        }
    }

    private static DefaultHttpContext MakeContext(string method, string path, string? idempotencyKey)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "test-user")], "Test"));
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

    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _source = new(bytes);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _source.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _source.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _source.Dispose(); base.Dispose(disposing); }
    }

    private sealed class FakeIdempotencyStore(IdempotencyOutcome outcome, IdempotentResponse? stored = null) : IIdempotencyStore
    {
        public int BeginCalls { get; private set; }

        public string? RequestBodyHash { get; private set; }

        public IdempotentResponse? Completed { get; private set; }

        public Task<(IdempotencyOutcome Outcome, IdempotentResponse? StoredResponse)> TryBeginAsync(string key, string requestBodyHash, CancellationToken cancellationToken = default)
        {
            BeginCalls++;
            RequestBodyHash = requestBodyHash;
            return Task.FromResult((outcome, stored));
        }

        public int ReleaseCalls { get; private set; }

        public Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken = default)
        {
            Completed = response;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
        {
            ReleaseCalls++;
            return Task.CompletedTask;
        }
    }
}
