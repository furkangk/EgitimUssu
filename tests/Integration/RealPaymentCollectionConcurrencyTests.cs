using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Projections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealPaymentCollectionConcurrencyTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Concurrent_Collections_Preserve_Both_Or_Reject_One_Without_Orphan_Movement()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var env = RealInfrastructure.Use(fixture);
        var barrier = new WriteBarrier();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddDbContext<PaymentsDbContext>(o => o.AddInterceptors(barrier))));
        using var client = factory.CreateClient();
        var teacher = await Login(client, "10.189.1.1");
        var payment = await CreatePayment(client, teacher);
        barrier.PaymentId = payment;
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/payments/records/{payment}/collections", Movement(250)),
            client.PostAsJsonAsync($"/api/payments/records/{payment}/collections", Movement(250)));
        try
        {
            Assert.All(responses, x => Assert.Contains(x.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
            Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.OK);
            var successes = responses.Count(x => x.StatusCode == HttpStatusCode.OK);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var stored = await db.PaymentRecords.SingleAsync(x => x.Id == payment);
            Assert.Equal(successes * 250m, stored.CollectedAmount);
            Assert.Equal(successes, stored.Transactions.Count);
            Assert.Equal(successes, await db.OutboxMessages.CountAsync(x => x.Type == nameof(PaymentTransactionRecordedIntegrationEvent) && x.Payload.Contains(payment.ToString())));
            var failure = responses.SingleOrDefault(x => x.StatusCode == HttpStatusCode.Conflict);
            if (failure is not null) Assert.Equal("shared.concurrency_conflict", (await Json(failure)).GetProperty("code").GetString());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [SkippableFact]
    public async Task Parent_Confirmation_Stale_Put_Refund_And_Report_Rebuild_Preserve_Money()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var env = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var teacher = await Login(client, "10.189.2.1");
        var paymentId = await CreatePayment(client, teacher);
        var declarationId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var payment = await db.PaymentRecords.SingleAsync(x => x.Id == paymentId);
            db.ParentPaymentDeclarations.Add(new ParentPaymentDeclaration(declarationId, paymentId, Guid.NewGuid(), teacher, payment.StudentId, 500m, "havale", DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        using var confirm = await client.PostAsync($"/api/payments/payment-declarations/{declarationId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        using var stale = await client.PutAsJsonAsync($"/api/payments/records/{paymentId}", new
        {
            description = "Eski ekrandan not", currency = "USD", expectedAmount = 500m,
            collectedAmount = 0m, status = 1, dueDateUtc = DateTime.UtcNow.AddDays(2), notes = "güncel not"
        });
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        var updated = await Json(stale);
        Assert.Equal(500m, updated.GetProperty("collectedAmount").GetDecimal());
        Assert.Equal("Paid", updated.GetProperty("status").GetString());
        Assert.Equal("TRY", updated.GetProperty("currency").GetString());
        using var refund = await client.PostAsJsonAsync($"/api/payments/records/{paymentId}/refunds", Movement(100));
        Assert.Equal(HttpStatusCode.OK, refund.StatusCode);
        var refundPayload = await Json(refund);
        Assert.Equal(400m, refundPayload.GetProperty("collectedAmount").GetDecimal());
        Assert.True(refundPayload.TryGetProperty("transactions", out var history));
        Assert.Equal(2, history.GetArrayLength());
        Assert.Equal("ParentDeclaration", history[0].GetProperty("method").GetString());
        Assert.Equal("Refund", history[1].GetProperty("kind").GetString());
        using var cancel = await client.PostAsync($"/api/payments/records/{paymentId}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);

        var stream = await PaymentEvents(factory, paymentId);
        await PublishReporting(factory, stream);
        await AssertCollected(factory, teacher, 400m);
        await PublishReporting(factory, stream.Select(x => x with { EventId = Guid.NewGuid() }).Reverse().ToArray());
        await AssertCollected(factory, teacher, 400m);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var rebuilder = Assert.Single(scope.ServiceProvider.GetServices<IProjectionRebuilder>(), x => x.Name == "reporting.payment-collections");
            Assert.True(await rebuilder.RebuildAsync(default) >= 2);
        }
        await AssertCollected(factory, teacher, 400m);
        await PublishReporting(factory, stream.Select(x => x with { EventId = Guid.NewGuid() }).ToArray());
        await AssertCollected(factory, teacher, 400m);
    }

    [SkippableFact]
    public async Task New_Write_Endpoints_Require_Owning_Teacher_And_Live_Private_Link()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var env = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient();
        var teacher = await Login(owner, "10.189.3.1");
        var payment = await CreatePayment(owner, teacher);
        using var stranger = factory.CreateClient();
        await Login(stranger, "10.189.3.2");
        foreach (var route in new[] { "collections", "refunds", "cancel" })
        {
            using var response = route == "cancel" ? await stranger.PostAsync($"/api/payments/records/{payment}/{route}", null)
                : await stranger.PostAsJsonAsync($"/api/payments/records/{payment}/{route}", Movement(100));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using var read = await owner.GetAsync($"/api/payments/records/{payment}");
        var studentId = (await Json(read)).GetProperty("studentId").GetGuid();
        using var archive = await owner.PostAsync($"/api/students/teachers/{teacher}/students/{studentId}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        foreach (var route in new[] { "collections", "refunds", "cancel" })
        {
            using var response = route == "cancel" ? await owner.PostAsync($"/api/payments/records/{payment}/{route}", null)
                : await owner.PostAsJsonAsync($"/api/payments/records/{payment}/{route}", Movement(100));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [SkippableFact]
    public async Task Migration_Backfills_Existing_Collection_And_Down_Up_Preserves_Net()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        var connection = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = $"ledger_{Guid.NewGuid():N}" };
        await using var admin = new NpgsqlConnection(fixture.PostgresConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new PaymentsDbContext(new DbContextOptionsBuilder<PaymentsDbContext>().UseNpgsql(connection.ConnectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "payments")).Options, new JsonDomainEventMapper());
            var all = db.Database.GetMigrations().ToArray();
            var target = Array.FindIndex(all, x => x.EndsWith("_PaymentTransactions", StringComparison.Ordinal));
            Assert.True(target > 0);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(all[target - 1]);
            var id = Guid.NewGuid();
            var teacher = Guid.NewGuid();
            var student = Guid.NewGuid();
            var due = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
            var collected = due.AddDays(2);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO payments.payment_records
                ("Id","TeacherUserId","StudentId","ItemType","Description","Currency","ExpectedAmount","CollectedAmount","DueDateUtc","CollectedOnUtc","Status")
                VALUES ({id},{teacher},{student},'LessonFee','eski','TRY',500,300,{due},{collected},'PartiallyPaid')
                """);
            var inconsistentId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO payments.payment_records
                ("Id","TeacherUserId","StudentId","ItemType","Description","Currency","ExpectedAmount","CollectedAmount","DueDateUtc","Status")
                VALUES ({inconsistentId},{teacher},{student},'LessonFee','tutarsız','TRY',500,0,{due},'Paid')
                """);
            await migrator.MigrateAsync();
            var normalized = await db.PaymentRecords.SingleAsync(x => x.Id == inconsistentId);
            Assert.Equal(PaymentStatus.Pending, normalized.Status);
            Assert.True(normalized.ValidateParentConfirmation(500m).IsSuccess);
            var payment = await db.PaymentRecords.SingleAsync(x => x.Id == id);
            var transaction = Assert.Single(payment.Transactions);
            Assert.Equal(PaymentMethod.Migrated, transaction.Method);
            Assert.Equal(PaymentTransactionKind.Collection, transaction.Kind);
            Assert.Equal(300m, transaction.Amount);
            Assert.Equal(collected, transaction.OccurredOnUtc);
            Assert.Equal(300m, payment.CollectedAmount);
            payment.Refund(Guid.NewGuid(), 100m, PaymentMethod.Cash, collected.AddDays(1), null, collected.AddDays(1));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await migrator.MigrateAsync(all[target - 1]);
            await migrator.MigrateAsync();
            var restored = await db.PaymentRecords.SingleAsync(x => x.Id == id);
            Assert.Equal(200m, restored.CollectedAmount);
            Assert.Equal(200m, Assert.Single(restored.Transactions).Amount);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [SkippableFact]
    public async Task Startup_Converts_Legacy_Monthly_Total_Before_New_Movement_And_Legacy_Delta()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var env = RealInfrastructure.Use(fixture);
        var connection = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = $"ledger_startup_{Guid.NewGuid():N}" };
        await using var admin = new NpgsqlConnection(fixture.PostgresConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin)) await create.ExecuteNonQueryAsync();
        var teacher = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        try
        {
            await using (var payments = new PaymentsDbContext(new DbContextOptionsBuilder<PaymentsDbContext>().UseNpgsql(connection.ConnectionString,
                o => o.MigrationsHistoryTable("__ef_migrations_history", "payments")).Options, new JsonDomainEventMapper()))
            {
                var all = payments.Database.GetMigrations().ToArray();
                var target = Array.FindIndex(all, x => x.EndsWith("_PaymentTransactions", StringComparison.Ordinal));
                await payments.GetService<IMigrator>().MigrateAsync(all[target - 1]);
                var student = Guid.NewGuid();
                await payments.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO payments.payment_records
                    ("Id","TeacherUserId","StudentId","ItemType","Description","Currency","ExpectedAmount","CollectedAmount","DueDateUtc","CollectedOnUtc","Status")
                    VALUES ({paymentId},{teacher},{student},'LessonFee','eski','TRY',1000,500,{now},{now},'PartiallyPaid')
                    """);
            }
            await using (var reporting = new ReportingDbContext(new DbContextOptionsBuilder<ReportingDbContext>().UseNpgsql(connection.ConnectionString,
                o => o.MigrationsHistoryTable("__ef_migrations_history", "reporting")).Options, new JsonDomainEventMapper()))
            {
                await reporting.Database.MigrateAsync();
                var local = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"));
                var projection = new EgitimUssu.Modules.Reporting.Domain.TeacherMonthlyProjection(teacher, local.Year, local.Month);
                projection.AddCollected(500m);
                reporting.Add(projection);
                await reporting.SaveChangesAsync();
            }
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", connection.ConnectionString);
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            await AssertCollected(factory, teacher, 500m);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
                var payment = await db.PaymentRecords.SingleAsync(x => x.Id == paymentId);
                payment.AddCollection(Guid.NewGuid(), 100m, PaymentMethod.Cash, now, null, DateTime.UtcNow);
                await db.SaveChangesAsync();
            }
            await PublishReporting(factory, await PaymentEvents(factory, paymentId));
            await AssertCollected(factory, teacher, 600m);
            await PublishReporting(factory, [new IntegrationEvent(Guid.NewGuid(), now, "PaymentRecordUpdatedDomainEvent", "Payments",
                JsonSerializer.Serialize(new { paymentRecordId = paymentId, teacherUserId = teacher, previousCollectedAmount = 0m, currentCollectedAmount = 500m, updatedOnUtc = now }))]);
            await AssertCollected(factory, teacher, 600m);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", fixture.PostgresConnectionString);
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [SkippableTheory]
    [InlineData(1.001, false)]
    [InlineData(100, true)]
    public async Task Initial_Collection_Rejects_Fractional_Minor_Units_And_Future_Date(decimal amount, bool future)
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var env = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var teacher = await Login(client, "10.189.4.1");
        var id = await CreatePayment(client, teacher);
        using var read = await client.GetAsync($"/api/payments/records/{id}");
        var student = (await Json(read)).GetProperty("studentId").GetGuid();
        using var invalid = await client.PostAsJsonAsync("/api/payments/records", new
        {
            teacherUserId = teacher, studentId = student, itemType = 1, description = "Hatalı", currency = "TRY", expectedAmount = 500m,
            collectedAmount = amount, collectedOnUtc = DateTime.UtcNow.AddDays(future ? 1 : -1), dueDateUtc = DateTime.UtcNow.AddDays(2), status = 3
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static object Movement(decimal amount) => new { amount, method = 1, occurredOnUtc = DateTime.UtcNow, note = "tahsilat" };
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }
    private static async Task<Guid> Login(HttpClient client, string ip)
    {
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        using var response = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"ledger-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Ledger", lastName = "Test", roles = new[] { 2 }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await Json(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return auth.GetProperty("userId").GetGuid();
    }
    private static async Task<Guid> CreatePayment(HttpClient client, Guid teacher)
    {
        using var student = await client.PostAsJsonAsync("/api/students/profiles", new
        { createdByTeacherUserId = teacher, fullName = "Ledger Öğrenci", gradeLevel = "8. Sınıf", origin = 1, subjects = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.OK, student.StatusCode);
        using var payment = await client.PostAsJsonAsync("/api/payments/records", new
        {
            teacherUserId = teacher, studentId = (await Json(student)).GetProperty("id").GetGuid(), itemType = 1,
            description = "Ders", currency = "TRY", expectedAmount = 500m, collectedAmount = 0m,
            status = 3, dueDateUtc = DateTime.UtcNow.AddDays(1)
        });
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);
        Assert.Equal("Pending", (await Json(payment)).GetProperty("status").GetString());
        return (await Json(payment)).GetProperty("id").GetGuid();
    }
    private static async Task<IntegrationEvent[]> PaymentEvents(WebApplicationFactory<Program> factory, Guid payment)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().OutboxMessages
            .Where(x => x.Payload.Contains(payment.ToString())).OrderBy(x => x.OccurredOnUtc).ToArrayAsync();
        return rows.Select(x => JsonSerializer.Deserialize<IntegrationEvent>(x.Payload, IntegrationEventSerialization.Options)!).ToArray();
    }
    private static async Task PublishReporting(WebApplicationFactory<Program> factory, IReadOnlyCollection<IntegrationEvent> stream)
    {
        foreach (var item in stream)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            foreach (var handler in scope.ServiceProvider.GetServices<IIntegrationEventHandler>()
                .Where(x => x.GetType().Assembly == typeof(ReportingDbContext).Assembly && x.CanHandle(item)))
                await handler.HandleAsync(item, default);
        }
    }
    private static async Task AssertCollected(WebApplicationFactory<Program> factory, Guid teacher, decimal expected)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(expected, await scope.ServiceProvider.GetRequiredService<ReportingDbContext>().TeacherMonthlyProjections
            .Where(x => x.TeacherUserId == teacher).SumAsync(x => x.CollectedAmount));
    }
    private sealed class WriteBarrier : SaveChangesInterceptor
    {
        public Guid PaymentId { get; set; }
        private int _arrivals;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (PaymentId != Guid.Empty && eventData.Context!.ChangeTracker.Entries<PaymentRecord>().Any(x => x.Entity.Id == PaymentId && x.State == EntityState.Modified))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _both.TrySetResult();
                await _both.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
            return result;
        }
    }
}
