using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Projections;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgitimUssu.Tests.Unit;

public sealed class ProjectionHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);

    private static TestModuleDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IntegrationEvent Event(Guid eventId, CounterPayload payload) =>
        new(eventId, DateTime.UnixEpoch, "CounterChangedDomainEvent", "Test",
            JsonSerializer.Serialize(payload, IntegrationEventSerialization.Options));

    private sealed record CounterPayload(Guid OwnerId, int Amount);

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class CounterProjectionHandler : ProjectionHandler<CounterPayload>
    {
        public CounterProjectionHandler(TestModuleDbContext dbContext, IClock clock)
            : base(dbContext, clock)
        {
        }

        public int ProjectCount { get; private set; }

        public bool Throw { get; set; }

        public override bool CanHandle(IIntegrationEvent integrationEvent)
            => integrationEvent.Name == "CounterChangedDomainEvent";

        protected override async Task<bool> ProjectAsync(
            CounterPayload payload,
            IntegrationEvent envelope,
            CancellationToken cancellationToken)
        {
            ProjectCount++;
            if (Throw)
            {
                throw new InvalidOperationException("boom");
            }

            var db = (TestModuleDbContext)DbContext;
            var projection = await db.CounterProjections
                .FirstOrDefaultAsync(item => item.OwnerId == payload.OwnerId, cancellationToken);
            if (projection is null)
            {
                projection = new TestCounterProjection { Id = Guid.NewGuid(), OwnerId = payload.OwnerId };
                db.CounterProjections.Add(projection);
            }

            projection.Add(payload.Amount);
            projection.MarkApplied(envelope.EventId, Clock.UtcNow);
            return true;
        }
    }

    [Fact]
    public async Task Same_Event_Twice_Should_Project_Once()
    {
        await using var db = NewContext();
        var handler = new CounterProjectionHandler(db, new FixedClock());
        var owner = Guid.NewGuid();
        var envelope = Event(Guid.NewGuid(), new CounterPayload(owner, 3));

        await handler.HandleAsync(envelope);
        await handler.HandleAsync(envelope);

        Assert.Equal(1, handler.ProjectCount);
        var projection = await db.CounterProjections.SingleAsync();
        Assert.Equal(3, projection.Count);
        Assert.Equal(1, await db.Set<InboxMessage>().CountAsync());
    }

    [Fact]
    public async Task Failed_Projection_Should_Not_Write_Inbox_Row()
    {
        await using var db = NewContext();
        var handler = new CounterProjectionHandler(db, new FixedClock()) { Throw = true };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(Event(Guid.NewGuid(), new CounterPayload(Guid.NewGuid(), 3))));

        Assert.Equal(0, await db.Set<InboxMessage>().CountAsync());
        Assert.Equal(0, await db.CounterProjections.CountAsync());
    }

    [Fact]
    public async Task Unreadable_Payload_Should_Not_Project()
    {
        await using var db = NewContext();
        var handler = new CounterProjectionHandler(db, new FixedClock());
        var envelope = new IntegrationEvent(
            Guid.NewGuid(), DateTime.UnixEpoch, "CounterChangedDomainEvent", "Test", "null");

        await Assert.ThrowsAsync<PoisonMessageException>(() => handler.HandleAsync(envelope));

        Assert.Equal(0, handler.ProjectCount);
        Assert.Equal(0, await db.Set<InboxMessage>().CountAsync());
    }

    [Fact]
    public void MarkApplied_Should_Record_Event_Id()
    {
        var projection = new TestCounterProjection { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() };
        var eventId = Guid.NewGuid();

        projection.MarkApplied(eventId, Now);

        Assert.Equal(eventId, projection.LastAppliedEventId);
        Assert.Equal(Now, projection.UpdatedOnUtc);
    }
}
