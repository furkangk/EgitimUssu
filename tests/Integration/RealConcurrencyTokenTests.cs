using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Observability;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealConcurrencyTokenTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Stale_Update_Is_Rejected_And_Losing_Outbox_Event_Is_Rolled_Back()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var env = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var payment = NewPayment();
        await SeedAsync(factory, payment);
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var a = await first.PaymentRecords.SingleAsync(x => x.Id == payment.Id);
        var b = await second.PaymentRecords.SingleAsync(x => x.Id == payment.Id);
        Update(a, "winner", 250m);
        Update(b, "loser", 100m);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var stored = await verify.PaymentRecords.SingleAsync(x => x.Id == payment.Id);
        Assert.NotEqual(0u, stored.Version);
        Assert.NotEqual(b.Version, stored.Version);
        Assert.Equal("winner", stored.Description);
        Assert.Equal(250m, stored.CollectedAmount);
        Assert.Equal(2, await verify.OutboxMessages.CountAsync(x => x.Type == nameof(PaymentRecordUpdatedDomainEvent)
            && x.Payload.Contains(payment.Id.ToString())));
    }

    [SkippableFact]
    public async Task Concurrent_Http_Updates_Return_One_Success_And_One_409()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var env = RealInfrastructure.Use(fixture);
        var barrier = new PaymentWriteBarrier();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddDbContext<PaymentsDbContext>(options => options.AddInterceptors(barrier))));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.188.1.1");
        using var registration = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"concurrency-{Guid.NewGuid():N}@test.com", password = "Passw0rd!",
            firstName = "Concurrency", lastName = "Test", roles = new[] { 2 }
        });
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        using var auth = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.RootElement.GetProperty("accessToken").GetString());
        var teacherId = auth.RootElement.GetProperty("userId").GetGuid();
        using var studentResponse = await client.PostAsJsonAsync("/api/students/profiles", new
        {
            createdByTeacherUserId = teacherId, fullName = "Concurrency Ogrenci", gradeLevel = "8. Sınıf", origin = 1,
            subjects = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.OK, studentResponse.StatusCode);
        using var studentJson = JsonDocument.Parse(await studentResponse.Content.ReadAsStringAsync());
        var payment = NewPayment(teacherId, studentJson.RootElement.GetProperty("id").GetGuid());
        await SeedAsync(factory, payment);
        barrier.PaymentId = payment.Id;

        var responses = await Task.WhenAll(
            client.PutAsJsonAsync($"/api/payments/records/{payment.Id}", Payload("first", 250m)),
            client.PutAsJsonAsync($"/api/payments/records/{payment.Id}", Payload("second", 100m)));
        try
        {
            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(x => x.StatusCode).OrderBy(x => x));
            using var error = JsonDocument.Parse(await responses.Single(x => x.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync());
            Assert.Equal("shared.concurrency_conflict", error.RootElement.GetProperty("code").GetString());
            Assert.Equal("Kayıt siz düzenlerken değişti. Lütfen yenileyip tekrar deneyin.", error.RootElement.GetProperty("message").GetString());
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var stored = await db.PaymentRecords.SingleAsync(x => x.Id == payment.Id);
            using var success = JsonDocument.Parse(await responses.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadAsStringAsync());
            Assert.Equal(success.RootElement.GetProperty("collectedAmount").GetDecimal(), stored.CollectedAmount);
            Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.Type == nameof(PaymentRecordUpdatedDomainEvent) && x.Payload.Contains(payment.Id.ToString())));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [SkippableFact]
    public async Task Outbox_Conflict_Rolls_Back_Inbox_And_Retry_Reloads_Current_State_Once()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var env = RealInfrastructure.Use(fixture);
        // Outbox partisi başka testlerin mesajlarını sahiplenmesin: fixture container'ında
        // yalnız bu senaryo için ayrı veritabanı; container kapanınca otomatik temizlenir.
        var isolated = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString)
        {
            Database = $"concurrency_{Guid.NewGuid():N}"
        };
        await using (var admin = new NpgsqlConnection(fixture.PostgresConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{isolated.Database}\"", admin);
            await create.ExecuteNonQueryAsync();
        }
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", isolated.ConnectionString);
        var state = new ConflictState();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(state);
            services.AddScoped<IIntegrationEventHandler, ConflictingPaymentHandler>();
        }));
        var payment = NewPayment();
        state.PaymentId = payment.Id;
        await SeedAsync(factory, payment);
        var now = DateTime.UtcNow;
        var eventId = Guid.NewGuid();
        var envelope = new IntegrationEvent(eventId, now, EventName, "Payments", "{}");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            // Existing payment-create events stay out of this controlled batch.
            await db.OutboxMessages.ExecuteUpdateAsync(s => s.SetProperty(x => x.ProcessedOnUtc, now));
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = eventId, Module = "Payments", Type = EventName,
                Payload = JsonSerializer.Serialize(envelope, IntegrationEventSerialization.Options), OccurredOnUtc = now
            });
            await db.SaveChangesAsync();
        }
        var clock = new TestClock(now);
        var bus = factory.Services.GetRequiredService<IEventBus>();
        var store = new EfOutboxStore(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OutboxOptions { BatchSize = 1000, MaxRetryCount = 5, RetryBackoffBaseSeconds = 1 }), clock,
            factory.Services.GetRequiredService<EgitimUssuMetrics>(), NullLogger<EfOutboxStore>.Instance);
        Task Publish(OutboxBatchItem item, CancellationToken ct) => bus.PublishAsync(
            JsonSerializer.Deserialize<IntegrationEvent>(item.Payload, IntegrationEventSerialization.Options)!, ct);

        await store.ProcessPendingAsync(Publish);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var message = await db.OutboxMessages.SingleAsync(x => x.Id == eventId);
            Assert.Null(message.ProcessedOnUtc);
            Assert.Null(message.DeadLetteredOnUtc);
            Assert.Equal(1, message.RetryCount);
            Assert.True(message.NextAttemptUtc > now);
            Assert.False(await db.InboxMessages.AnyAsync(x => x.EventId == eventId));
            Assert.Equal(100m, (await db.PaymentRecords.SingleAsync(x => x.Id == payment.Id)).CollectedAmount);
        }
        clock.UtcNow = now.AddSeconds(2);
        await store.ProcessPendingAsync(Publish);
        // Duplicate delivery is harmless after the successful retry.
        await bus.PublishAsync(envelope);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            Assert.NotNull((await db.OutboxMessages.SingleAsync(x => x.Id == eventId)).ProcessedOnUtc);
            Assert.Equal(1, await db.InboxMessages.CountAsync(x => x.EventId == eventId));
            Assert.Equal(350m, (await db.PaymentRecords.SingleAsync(x => x.Id == payment.Id)).CollectedAmount);
            Assert.Equal(2, state.Attempts);
        }
    }

    private const string EventName = "ConcurrencyTestPaymentAdjustment";
    private sealed class ConflictState { public Guid PaymentId; public int Attempts; }
    private sealed class TestClock(DateTime now) : IClock { public DateTime UtcNow { get; set; } = now; }

    private sealed class ConflictingPaymentHandler(PaymentsDbContext db, IClock clock, IServiceScopeFactory scopes, ConflictState state)
        : IdempotentIntegrationEventHandler(db, clock)
    {
        public override bool CanHandle(IIntegrationEvent integrationEvent) => integrationEvent.Name == EventName;
        protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
        {
            var payment = await db.PaymentRecords.SingleAsync(x => x.Id == state.PaymentId, cancellationToken);
            if (Interlocked.Increment(ref state.Attempts) == 1)
            {
                await using var competingScope = scopes.CreateAsyncScope();
                var competing = competingScope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
                var latest = await competing.PaymentRecords.SingleAsync(x => x.Id == state.PaymentId, cancellationToken);
                Update(latest, "competing collection", 100m);
                await competing.SaveChangesAsync(cancellationToken);
            }
            Update(payment, "outbox adjustment", 250m);
            return true;
        }
    }

    private sealed class PaymentWriteBarrier : SaveChangesInterceptor
    {
        public Guid PaymentId;
        private int _arrivals;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (PaymentId != Guid.Empty && eventData.Context!.ChangeTracker.Entries<PaymentRecord>()
                .Any(x => x.Entity.Id == PaymentId && x.State == EntityState.Modified))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _both.TrySetResult();
                await _both.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }

    private static PaymentRecord NewPayment(Guid? teacherId = null, Guid? studentId = null) => new(
        Guid.NewGuid(), teacherId ?? Guid.NewGuid(), studentId ?? Guid.NewGuid(), null, BillingItemType.LessonFee,
        "concurrency seed", "TRY", 500m, 0m, DateTime.UtcNow.AddDays(1), null, PaymentStatus.Pending, null, null, null, DateTime.UtcNow);

    private static void Update(PaymentRecord payment, string description, decimal amount)
    {
        payment.UpdateDetails(description, 500m, payment.DueDateUtc, null, DateTime.UtcNow);
        payment.AddCollection(Guid.NewGuid(), amount, PaymentMethod.Cash, DateTime.UtcNow, null, DateTime.UtcNow);
    }

    private static object Payload(string description, decimal amount) => new
    {
        itemType = 1, description, currency = "TRY", expectedAmount = 500m, collectedAmount = amount,
        dueDateUtc = DateTime.UtcNow.AddDays(1), collectedOnUtc = DateTime.UtcNow, status = 2
    };

    private static async Task SeedAsync(WebApplicationFactory<Program> factory, PaymentRecord payment)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        db.PaymentRecords.Add(payment);
        await db.SaveChangesAsync();
    }
}
