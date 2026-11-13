using System.Text.Json;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// M14/K5: Outbox işlemenin gerçek Postgres'e karşı doğrulanması — Npgsql <c>FOR UPDATE SKIP LOCKED</c>
/// claim yolu, mesaj-başına retry ve dead-letter (Aşama 1'de yalnız derlemede doğrulanmıştı).
/// </summary>
[Collection("containers")]
public sealed class RealOutboxIntegrationTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Poison_Message_Is_Dead_Lettered_On_Real_Postgres_Without_Blocking_Sibling()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        // MaxRetryCount=1 → ilk başarısızlıkta dead-letter.
        using var envScope = RealInfrastructure.Use(fixture, maxRetryCount: 1);
        await using var factory = new WebApplicationFactory<Program>();

        var goodId = Guid.NewGuid();
        var poisonId = Guid.NewGuid();

        // Geçerli ama hiçbir handler'ın eşleşmediği event → başarıyla "yayınlanır" → işlenir.
        var goodEvent = new IntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, "M14NoHandlerEvent", "Test", "{}");
        var goodPayload = JsonSerializer.Serialize(goodEvent, IntegrationEventSerialization.Options);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            db.OutboxMessages.Add(NewMessage(goodId, goodPayload, DateTime.UtcNow));
            // "null" payload → deserialize null → publish fırlatır (zehirli mesaj).
            db.OutboxMessages.Add(NewMessage(poisonId, "null", DateTime.UtcNow.AddSeconds(1)));
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
            await processor.DispatchPendingAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var good = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == goodId);
            var poison = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == poisonId);

            // Zehirli mesaja rağmen sağlıklı mesaj işlendi (SKIP LOCKED claim + per-mesaj işleme).
            Assert.NotNull(good.ProcessedOnUtc);

            // Zehirli mesaj dead-letter'a taşındı, kuyruğu bloklamadı.
            Assert.NotNull(poison.DeadLetteredOnUtc);
            Assert.Null(poison.ProcessedOnUtc);
            Assert.Equal(1, poison.RetryCount);
            Assert.NotNull(poison.Error);
        }
    }

    /// <summary>
    /// A-09: İşlenmiş satırlar budanır; <b>bekleyen</b> ve <b>dead-letter</b> satırlara dokunulmaz.
    /// <para>
    /// Outbox ve inbox tabloları 15 modülün her birinde tek yönde büyüyordu — satır işlendi diye
    /// işaretleniyor ama hiç silinmiyordu. Dead-letter satırları bilerek korunur: incelenmek için
    /// oradadırlar, budama onları da silseydi arıza kaydı kaybolurdu.
    /// </para>
    /// </summary>
    [SkippableFact]
    public async Task Retention_Removes_Processed_Rows_But_Keeps_Pending_And_Dead_Lettered()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var envScope = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var oldProcessedId = Guid.NewGuid();
        var freshProcessedId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        var deadLetteredId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();

            var oldProcessed = NewMessage(oldProcessedId, "{}", now.AddDays(-40));
            oldProcessed.ProcessedOnUtc = now.AddDays(-40);

            var freshProcessed = NewMessage(freshProcessedId, "{}", now.AddDays(-1));
            freshProcessed.ProcessedOnUtc = now.AddDays(-1);

            var deadLettered = NewMessage(deadLetteredId, "null", now.AddDays(-40));
            deadLettered.DeadLetteredOnUtc = now.AddDays(-40);

            db.OutboxMessages.AddRange(oldProcessed, freshProcessed, NewMessage(pendingId, "{}", now), deadLettered);
            db.Set<InboxMessage>().Add(new InboxMessage(Guid.NewGuid(), "A09Handler", "A09Event", now.AddDays(-40)));
            db.Set<InboxMessage>().Add(new InboxMessage(Guid.NewGuid(), "A09Handler", "A09Event", now.AddDays(-1)));
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
            await processor.PurgeProcessedAsync(TimeSpan.FromDays(14));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var ids = await db.OutboxMessages.AsNoTracking().Select(message => message.Id).ToListAsync();

            Assert.DoesNotContain(oldProcessedId, ids);
            Assert.Contains(freshProcessedId, ids);
            Assert.Contains(pendingId, ids);
            Assert.Contains(deadLetteredId, ids);

            var inbox = await db.Set<InboxMessage>().AsNoTracking()
                .Where(message => message.Handler == "A09Handler")
                .ToListAsync();
            Assert.Single(inbox);
        }
    }

    private static OutboxMessage NewMessage(Guid id, string payload, DateTime occurredOnUtc) => new()
    {
        Id = id,
        Module = "Test",
        Type = "M14",
        Payload = payload,
        OccurredOnUtc = occurredOnUtc,
    };
}
