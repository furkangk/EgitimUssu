using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
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
public sealed class RealPaymentDeclarationUniquenessTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Concurrent_http_declarations_have_one_winner_and_one_409_and_rejection_allows_retry()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var env = RealInfrastructure.Use(fixture);
        var barrier = new DeclarationBarrier();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddDbContext<PaymentsDbContext>(o => o.AddInterceptors(barrier))));
        using var teacher = factory.CreateClient();
        var owner = await Login(teacher, 2, "10.190.1.1");
        using var parent = factory.CreateClient();
        var parentId = await Login(parent, 4, "10.190.1.2");
        var payment = await CreatePayment(teacher, owner);
        using var read = await teacher.GetAsync($"/api/payments/records/{payment}");
        var student = (await Json(read)).GetProperty("studentId").GetGuid();
        using var invite = await teacher.PostAsJsonAsync($"/api/students/profiles/{student}/parent-invite", new { teacherUserId = owner, childDisplayName = "Beyan Öğrenci" });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        using var claim = await parent.PostAsJsonAsync("/api/parents/children/claim-invite", new { inviteCode = (await Json(invite)).GetProperty("inviteCode").GetString() });
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        barrier.PaymentId = payment;
        var replies = await Task.WhenAll(Declare(parent, payment), Declare(parent, payment));
        try
        {
            var success = Assert.Single(replies, x => x.StatusCode == HttpStatusCode.OK);
            var conflict = Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("payments.declaration_pending", (await Json(conflict)).GetProperty("code").GetString());
            var id = (await Json(success)).GetProperty("id").GetGuid();
            barrier.PaymentId = Guid.Empty;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
                Assert.Single(await db.ParentPaymentDeclarations.Where(x => x.PaymentRecordId == payment).ToArrayAsync());
                Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.Type == nameof(ParentPaymentDeclaredDomainEvent) && x.Payload.Contains(payment.ToString())));
            }
            using var reject = await teacher.PostAsync($"/api/payments/payment-declarations/{id}/reject", null);
            Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
            await DeliverResolutions(factory, payment);
            await DeliverResolutions(factory, payment);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var notice = Assert.Single(await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().UserNotifications.Where(x => x.UserId == parentId).ToArrayAsync());
                Assert.Contains("reddedildi", notice.Body);
            }
            using var retry = await Declare(parent, payment);
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            var retryId = (await Json(retry)).GetProperty("id").GetGuid();
            using var confirm = await teacher.PostAsync($"/api/payments/payment-declarations/{retryId}/confirm", null);
            Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
            await DeliverResolutions(factory, payment);
            await using var verify = factory.Services.CreateAsyncScope();
            var stored = await verify.ServiceProvider.GetRequiredService<PaymentsDbContext>().PaymentRecords.SingleAsync(x => x.Id == payment);
            Assert.Equal(500m, stored.CollectedAmount);
            Assert.Single(stored.Transactions);
            Assert.Equal(2, await verify.ServiceProvider.GetRequiredService<NotificationsDbContext>().UserNotifications.CountAsync(x => x.UserId == parentId));
        }
        finally { foreach (var response in replies) response.Dispose(); }
    }

    [SkippableFact]
    public async Task Stale_rejection_cannot_overwrite_a_confirmed_declaration_or_its_money()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var env = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var teacher = await Login(client, 2, "10.190.2.1");
        var payment = await CreatePayment(client, teacher);
        Guid id;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var record = await db.PaymentRecords.SingleAsync(x => x.Id == payment);
            var declaration = new ParentPaymentDeclaration(Guid.NewGuid(), payment, Guid.NewGuid(), teacher, record.StudentId, 500m, null, DateTime.UtcNow);
            id = declaration.Id;
            db.Add(declaration);
            await db.SaveChangesAsync();
        }
        await using var staleScope = factory.Services.CreateAsyncScope();
        var staleDb = staleScope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var stale = await staleDb.ParentPaymentDeclarations.SingleAsync(x => x.Id == id);
        using var confirm = await client.PostAsync($"/api/payments/payment-declarations/{id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        stale.Reject(DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleDb.SaveChangesAsync());
        await using var scope = factory.Services.CreateAsyncScope();
        var fresh = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        Assert.Equal(ParentPaymentDeclarationStatus.Confirmed, (await fresh.ParentPaymentDeclarations.SingleAsync(x => x.Id == id)).Status);
        Assert.Equal(500m, (await fresh.PaymentRecords.SingleAsync(x => x.Id == payment)).CollectedAmount);
    }

    [SkippableFact]
    public async Task Migration_rejects_older_pending_only_notifies_each_loser_and_down_up_does_not_resurrect_them()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        var connection = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = $"declaration_{Guid.NewGuid():N}" };
        await using var admin = new NpgsqlConnection(fixture.PostgresConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new PaymentsDbContext(new DbContextOptionsBuilder<PaymentsDbContext>().UseNpgsql(connection.ConnectionString,
                o => o.MigrationsHistoryTable("__ef_migrations_history", "payments")).Options, new JsonDomainEventMapper());
            var all = db.Database.GetMigrations().ToArray();
            var index = Array.FindIndex(all, x => x.EndsWith("_SinglePendingDeclaration", StringComparison.Ordinal));
            Assert.True(index > 0, "SinglePendingDeclaration göçü bulunmalı.");
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(all[index - 1]);
            var payment = Guid.NewGuid();
            var teacher = Guid.NewGuid();
            var student = Guid.NewGuid();
            var old = Guid.NewGuid();
            var tied = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var kept = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var resolved = Guid.NewGuid();
            var now = DateTime.UtcNow;
            foreach (var row in new[] { (old, now.AddDays(-1), "Declared"), (tied, now, "Declared"), (kept, now, "Declared"), (resolved, now.AddDays(-3), "Confirmed") })
            {
                var id = row.Item1;
                var created = row.Item2;
                var status = row.Item3;
                var parent = Guid.NewGuid();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO payments.parent_payment_declarations
                    ("Id","PaymentRecordId","ParentUserId","TeacherUserId","StudentId","DeclaredAmount","Status","CreatedOnUtc","ResolvedOnUtc")
                    VALUES ({id},{payment},{parent},{teacher},{student},500,{status},{created},{(DateTime?)null})
                    """);
            }
            await migrator.MigrateAsync();
            var rows = await db.ParentPaymentDeclarations.ToArrayAsync();
            Assert.Equal(kept, Assert.Single(rows, x => x.Status == ParentPaymentDeclarationStatus.Declared).Id);
            Assert.Equal(2, rows.Count(x => x.Status == ParentPaymentDeclarationStatus.Rejected && x.ResolvedOnUtc != null));
            Assert.Equal(ParentPaymentDeclarationStatus.Confirmed, rows.Single(x => x.Id == resolved).Status);
            Assert.Equal(2, await db.OutboxMessages.CountAsync(x => x.Type == nameof(ParentPaymentDeclarationResolvedDomainEvent)));
            // SQL ile üretilen zarf gerçek tüketiciye gider; hem doğru alıcı hem inbox tekliği sınanır.
            await using (var notifications = new NotificationsDbContext(
                new DbContextOptionsBuilder<NotificationsDbContext>().UseNpgsql(connection.ConnectionString,
                    o => o.MigrationsHistoryTable("__ef_migrations_history", "notifications")).Options, new JsonDomainEventMapper()))
            {
                await notifications.Database.MigrateAsync();
                var consumer = new PaymentDeclarationResolvedNotificationHandler(notifications, new Ids(), new Clock());
                foreach (var row in await db.OutboxMessages.ToArrayAsync())
                {
                    var envelope = JsonSerializer.Deserialize<IntegrationEvent>(row.Payload, IntegrationEventSerialization.Options)!;
                    Assert.True(consumer.CanHandle(envelope));
                    await consumer.HandleAsync(envelope);
                    await consumer.HandleAsync(envelope);
                }
                var recipients = rows.Where(x => x.Status == ParentPaymentDeclarationStatus.Rejected).Select(x => x.ParentUserId).Order().ToArray();
                Assert.Equal(recipients, await notifications.UserNotifications.OrderBy(x => x.UserId).Select(x => x.UserId).ToArrayAsync());
            }
            var duplicate = new ParentPaymentDeclaration(Guid.NewGuid(), payment, Guid.NewGuid(), teacher, student, 500m, null, now);
            db.Add(duplicate);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)error.InnerException!).SqlState);
            db.ChangeTracker.Clear();
            await migrator.MigrateAsync(all[index - 1]);
            await migrator.MigrateAsync();
            Assert.Equal(2, await db.ParentPaymentDeclarations.CountAsync(x => x.Status == ParentPaymentDeclarationStatus.Rejected));
            Assert.Equal(2, await db.OutboxMessages.CountAsync(x => x.Type == nameof(ParentPaymentDeclarationResolvedDomainEvent)));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static Task<HttpResponseMessage> Declare(HttpClient client, Guid payment) => client.PostAsJsonAsync($"/api/payments/records/{payment}/declare-paid", new { declaredAmount = 500m });
    private static async Task<Guid> Login(HttpClient client, int role, string ip)
    {
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        using var response = await client.PostAsJsonAsync("/api/identity/register", new { email = $"declaration-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Beyan", lastName = "Test", roles = new[] { role } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return body.GetProperty("userId").GetGuid();
    }
    private static async Task<Guid> CreatePayment(HttpClient client, Guid teacher)
    {
        using var student = await client.PostAsJsonAsync("/api/students/profiles", new { createdByTeacherUserId = teacher, fullName = "Beyan Öğrenci", gradeLevel = "8. Sınıf", origin = 1, subjects = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.OK, student.StatusCode);
        using var payment = await client.PostAsJsonAsync("/api/payments/records", new { teacherUserId = teacher, studentId = (await Json(student)).GetProperty("id").GetGuid(), itemType = 1, description = "Ders", currency = "TRY", expectedAmount = 500m, collectedAmount = 0m, status = 3, dueDateUtc = DateTime.UtcNow.AddDays(1) });
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);
        return (await Json(payment)).GetProperty("id").GetGuid();
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }
    private static async Task DeliverResolutions(WebApplicationFactory<Program> factory, Guid payment)
    {
        IntegrationEvent[] events;
        await using (var scope = factory.Services.CreateAsyncScope())
            events = (await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().OutboxMessages.Where(x => x.Type == nameof(ParentPaymentDeclarationResolvedDomainEvent) && x.Payload.Contains(payment.ToString())).ToArrayAsync())
                .Select(x => JsonSerializer.Deserialize<IntegrationEvent>(x.Payload, IntegrationEventSerialization.Options)!).ToArray();
        foreach (var item in events)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var handler = Assert.Single(scope.ServiceProvider.GetServices<IIntegrationEventHandler>(), x => x.GetType().Assembly == typeof(NotificationsDbContext).Assembly && x.CanHandle(item));
            await handler.HandleAsync(item, default);
        }
    }
    private sealed class Clock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
    private sealed class Ids : IIdGenerator { public Guid New() => Guid.NewGuid(); }
    private sealed class DeclarationBarrier : SaveChangesInterceptor
    {
        public Guid PaymentId { get; set; }
        private int _arrivals;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (PaymentId != Guid.Empty && data.Context!.ChangeTracker.Entries<ParentPaymentDeclaration>().Any(x => x.Entity.PaymentRecordId == PaymentId && x.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _both.TrySetResult();
                await _both.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
            return result;
        }
    }
}
