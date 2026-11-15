using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Middleware;
using EgitimUssu.Shared.Infrastructure.Observability;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealIdempotencyBodyHashTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Completed_Request_Replays_Only_Identical_Body_And_Preserves_Original_Response()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var services = Services();
        using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.RedisConnectionString);
        var store = Store(connection, services);
        var key = Guid.NewGuid().ToString("N");
        var calls = 0;
        var middleware = new IdempotencyMiddleware(async ctx =>
        {
            calls++;
            using var reader = new StreamReader(ctx.Request.Body, leaveOpen: true);
            ctx.Response.StatusCode = 201;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(await reader.ReadToEndAsync());
        }, store);
        var first = Context(key, "{\"amount\":100}", services);
        var repeat = Context(key, "{\"amount\":100}", services);
        var changed = Context(key, "{\"amount\":200}", services);
        await middleware.InvokeAsync(first);
        await middleware.InvokeAsync(repeat);
        await middleware.InvokeAsync(changed);
        var afterRejection = Context(key, "{\"amount\":100}", services);
        await middleware.InvokeAsync(afterRejection);
        Assert.Equal(1, calls);
        Assert.Equal(201, repeat.Response.StatusCode);
        Assert.Equal("application/json", repeat.Response.ContentType);
        Assert.Equal(await Body(first), await Body(repeat));
        Assert.Equal(await Body(first), await Body(afterRejection));
        Assert.Equal(422, changed.Response.StatusCode);
        Assert.Contains("shared.idempotency_key_reused", await Body(changed));
    }

    [SkippableFact]
    public async Task Processing_Request_Rejects_Different_Body_And_Blocks_Same_Body_Without_Releasing_Owner()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var services = Services();
        using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.RedisConnectionString);
        var store = Store(connection, services);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var middleware = new IdempotencyMiddleware(async ctx =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await finish.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await ctx.Response.WriteAsync("saved");
        }, store);
        var key = Guid.NewGuid().ToString("N");
        var owner = middleware.InvokeAsync(Context(key, "100", services));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var same = Context(key, "100", services);
            var different = Context(key, "200", services);
            await Task.WhenAll(middleware.InvokeAsync(same), middleware.InvokeAsync(different));
            Assert.Equal(409, same.Response.StatusCode);
            Assert.Equal(422, different.Response.StatusCode);
            var stillProcessing = Context(key, "100", services);
            await middleware.InvokeAsync(stillProcessing);
            Assert.Equal(409, stillProcessing.Response.StatusCode);
            Assert.Equal(1, calls);
        }
        finally
        {
            finish.TrySetResult();
            await owner;
        }
    }

    [SkippableFact]
    public async Task Concurrent_First_Reservations_Choose_One_Body_Atomically()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var services = Services();
        using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.RedisConnectionString);
        var store = Store(connection, services);
        var key = Guid.NewGuid().ToString("N");
        var tasks = Enumerable.Range(0, 20).Select(async index =>
        {
            var hash = index % 2 == 0 ? "body-a" : "body-b";
            var result = await store.TryBeginAsync(key, hash);
            return (Hash: hash, result.Outcome);
        });
        var results = await Task.WhenAll(tasks);
        var winner = Assert.Single(results, x => x.Outcome == IdempotencyOutcome.Proceed);
        Assert.All(results.Where(x => x.Outcome != IdempotencyOutcome.Proceed), item =>
            Assert.Equal(item.Hash == winner.Hash ? IdempotencyOutcome.InProgress : IdempotencyOutcome.BodyMismatch, item.Outcome));
        var ttl = await connection.GetDatabase().KeyTimeToLiveAsync("idempotency:" + key);
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value.TotalMinutes, 4, 5);
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_Record_Does_Not_Reexecute_Unknown_Body(bool processing)
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var services = Services();
        using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.RedisConnectionString);
        var key = Guid.NewGuid().ToString("N");
        var redisKey = $"idempotency:test-user:POST:/api/payments/records:{key}";
        var payload = processing ? "\u0001processing" : "{\"statusCode\":200,\"contentType\":\"application/json\",\"body\":\"e30=\"}";
        await connection.GetDatabase().StringSetAsync(redisKey, payload, TimeSpan.FromMinutes(1));
        var calls = 0;
        var middleware = new IdempotencyMiddleware(_ => { calls++; return Task.CompletedTask; }, Store(connection, services));
        var context = Context(key, "new-body", services);
        await middleware.InvokeAsync(context);
        Assert.Equal(processing ? 409 : 422, context.Response.StatusCode);
        Assert.Equal(0, calls);
        Assert.Equal(payload, (await connection.GetDatabase().StringGetAsync(redisKey)).ToString());
    }

    [SkippableFact]
    public async Task Failed_Request_Releases_Hash_And_Allows_Corrected_Request()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var services = Services();
        using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.RedisConnectionString);
        var calls = 0;
        var middleware = new IdempotencyMiddleware(ctx =>
        {
            ctx.Response.StatusCode = ++calls == 1 ? 400 : 200;
            return Task.CompletedTask;
        }, Store(connection, services));
        var key = Guid.NewGuid().ToString("N");
        await middleware.InvokeAsync(Context(key, "invalid", services));
        var corrected = Context(key, "valid", services);
        await middleware.InvokeAsync(corrected);
        Assert.Equal(200, corrected.Response.StatusCode);
        Assert.Equal(2, calls);
    }

    [SkippableFact]
    public async Task Http_Collection_Retry_Writes_One_Movement_And_Different_Body_Returns_422()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var env = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.189.55.1");
        using var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"hash-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Hash", lastName = "Test", roles = new[] { 2 }
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = await register.Content.ReadFromJsonAsync<JsonElement>();
        var teacher = auth.GetProperty("userId").GetGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        using var student = await client.PostAsJsonAsync("/api/students/profiles", new
        { createdByTeacherUserId = teacher, fullName = "Hash Öğrenci", gradeLevel = "8. Sınıf", origin = 1, subjects = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.OK, student.StatusCode);
        var studentId = (await student.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var payment = await client.PostAsJsonAsync("/api/payments/records", new
        {
            teacherUserId = teacher, studentId, itemType = 1, description = "Ders", currency = "TRY",
            expectedAmount = 500m, collectedAmount = 0m, status = 3, dueDateUtc = DateTime.UtcNow.AddDays(1)
        });
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);
        var paymentId = (await payment.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var occurred = DateTime.UtcNow.AddSeconds(-1);
        var body = JsonSerializer.Serialize(new { amount = 100m, method = 1, occurredOnUtc = occurred, note = "ilk" });
        using var first = await Send(body);
        using var repeated = await Send(body);
        using var changed = await Send(JsonSerializer.Serialize(new { amount = 200m, method = 1, occurredOnUtc = occurred, note = "ilk" }));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await repeated.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        Assert.Equal("shared.idempotency_key_reused", (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var record = await db.PaymentRecords.SingleAsync(x => x.Id == paymentId);
        Assert.Equal(100m, record.CollectedAmount);
        Assert.Single(record.Transactions);

        async Task<HttpResponseMessage> Send(string json)
            => await client.PostAsync($"/api/payments/records/{paymentId}/collections", new StringContent(json, Encoding.UTF8, "application/json"));
    }

    private static ServiceProvider Services() => new ServiceCollection().AddLogging().AddMetrics().AddSingleton<EgitimUssuMetrics>().BuildServiceProvider();
    private static RedisIdempotencyStore Store(IConnectionMultiplexer connection, IServiceProvider services)
        => new(new ResilientRedisExecutor(new Connection(connection), new Clock(), services.GetRequiredService<EgitimUssuMetrics>(), NullLogger<ResilientRedisExecutor>.Instance));
    private static DefaultHttpContext Context(string key, string body, IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], "Test"));
        context.Request.Method = "POST";
        context.Request.Path = "/api/payments/records";
        context.Request.Headers["Idempotency-Key"] = key;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Response.Body = new MemoryStream();
        return context;
    }
    private static async Task<string> Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
    private sealed class Connection(IConnectionMultiplexer connection) : IRedisConnectionFactory
    {
        public ValueTask<IConnectionMultiplexer> GetAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(connection);
    }
    private sealed class Clock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
}
