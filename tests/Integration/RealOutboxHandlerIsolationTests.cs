using System.Text.Json;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealOutboxHandlerIsolationTests(ContainerFixture fixture)
{
    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Four_Handlers_Commit_Independently_And_Retry_Only_Failed_Handler(bool recover)
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var environment = RealInfrastructure.Use(fixture, maxRetryCount: 2);
        var calls = new int[4];
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                for (var index = 0; index < 4; index++)
                {
                    var slot = index;
                    services.AddScoped<IIntegrationEventHandler>(s => new Probe(
                        s.GetRequiredService<NotificationsDbContext>(), s.GetRequiredService<IClock>(), slot, calls, recover));
                }
            }));
        var envelope = new IntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, "N42Isolation", "Test", "{}");
        var messageId = await Seed(factory, envelope);
        await Dispatch(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var notifications = await db.UserNotifications.Where(n => n.UserId == envelope.EventId).ToListAsync();
            Assert.Equal(3, notifications.Count);
            Assert.DoesNotContain(notifications, n => n.Title is "Probe1" or "Leaked stage");
            Assert.Equal(3, await db.InboxMessages.CountAsync(i => i.EventId == envelope.EventId));
            var source = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var message = await source.OutboxMessages.SingleAsync(m => m.Id == messageId);
            Assert.Null(message.ProcessedOnUtc);
            Assert.Equal(1, message.RetryCount);
            Assert.Null(message.DeadLetteredOnUtc);
            Assert.True(message.NextAttemptUtc > envelope.OccurredOnUtc);
            Assert.Contains(nameof(Probe), message.Error);
            // Gerçek backoff korunur; test beklemek yerine yalnız kendi satırını yeniden hazırlar.
            message.NextAttemptUtc = DateTime.UtcNow.AddSeconds(-1);
            await source.SaveChangesAsync();
        }
        await Dispatch(factory);
        Assert.Equal(new[] { 1, 2, 1, 1 }, calls);
        await using var read = factory.Services.CreateAsyncScope();
        var target = read.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        Assert.Equal(recover ? 4 : 3, await target.UserNotifications.CountAsync(n => n.UserId == envelope.EventId));
        Assert.Equal(recover ? 4 : 3, await target.InboxMessages.CountAsync(i => i.EventId == envelope.EventId));
        var final = await read.ServiceProvider.GetRequiredService<SchedulingDbContext>().OutboxMessages.SingleAsync(m => m.Id == messageId);
        if (recover)
        {
            Assert.NotNull(final.ProcessedOnUtc);
            Assert.Null(final.Error);
            Assert.Null(final.DeadLetteredOnUtc);
        }
        else
        {
            Assert.Null(final.ProcessedOnUtc);
            Assert.NotNull(final.DeadLetteredOnUtc);
            Assert.Equal(2, final.RetryCount);
            Assert.Contains(nameof(Probe), final.Error);
        }
    }

    [SkippableTheory]
    [InlineData("null")]
    [InlineData("{broken-secret-content")]
    [InlineData("{}")]
    public async Task Invalid_Message_Payload_Is_Dead_Lettered_With_Handler_Name_And_No_Inbox(string payload)
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var environment = RealInfrastructure.Use(fixture, maxRetryCount: 1);
        await using var factory = new WebApplicationFactory<Program>();
        var envelope = new IntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, "MessageSentDomainEvent", "Messaging", payload);
        var messageId = await Seed(factory, envelope);
        await Dispatch(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var message = await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().OutboxMessages.SingleAsync(m => m.Id == messageId);
        Assert.Null(message.ProcessedOnUtc);
        Assert.NotNull(message.DeadLetteredOnUtc);
        Assert.Contains(nameof(MessageSentNotificationHandler), message.Error);
        Assert.Contains(nameof(PoisonMessageException), message.Error);
        Assert.DoesNotContain("secret-content", message.Error);
        Assert.False(await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().InboxMessages.AnyAsync(i => i.EventId == envelope.EventId));
    }

    [SkippableFact]
    public async Task Every_Registered_Production_Handler_Uses_Inbox_Base()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var environment = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler>().ToArray();
        Assert.NotEmpty(handlers);
        Assert.All(handlers, handler => Assert.IsAssignableFrom<IdempotentIntegrationEventHandler>(handler));
    }

    private static async Task<Guid> Seed(WebApplicationFactory<Program> factory, IntegrationEvent envelope)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var id = Guid.NewGuid();
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = id, Module = envelope.SourceModule, Type = envelope.Name,
            Payload = JsonSerializer.Serialize(envelope, IntegrationEventSerialization.Options), OccurredOnUtc = envelope.OccurredOnUtc
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task Dispatch(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().DispatchPendingAsync();
    }

    private sealed class Probe(NotificationsDbContext db, IClock clock, int slot, int[] calls, bool recover)
        : IdempotentIntegrationEventHandler(db, clock)
    {
        protected override string HandlerName => $"N42Probe{slot}";
        public override bool CanHandle(IIntegrationEvent e) => e.Name == "N42Isolation";
        protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
        {
            calls[slot]++;
            db.UserNotifications.Add(Notification(envelope, $"Probe{slot}"));
            if (slot == 1 && (!recover || calls[slot] == 1))
            {
                // Bilinçli hata enjeksiyonu: SQL yazımı rollback, sonraki staged yazım tracker temizliği ister.
                await db.SaveChangesAsync(cancellationToken);
                db.UserNotifications.Add(Notification(envelope, "Leaked stage"));
                throw new InvalidOperationException("injected failure");
            }
            return true;
        }
        private UserNotification Notification(IntegrationEvent envelope, string title)
            => new(Guid.NewGuid(), envelope.EventId, UserNotificationKind.System, title, "N42", null, Clock.UtcNow);
    }
}
