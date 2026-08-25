using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgitimUssu.Tests.Unit;

public sealed class IdempotentIntegrationEventHandlerTests
{
    private static TestModuleDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IntegrationEvent Event(Guid id) =>
        new(id, DateTime.UnixEpoch, "SampleDomainEvent", "Study", "{}");

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => DateTime.UnixEpoch;
    }

    private sealed class CountingHandler : IdempotentIntegrationEventHandler
    {
        public CountingHandler(ModuleDbContext db, IClock clock, bool applyResult = true) : base(db, clock)
            => _applyResult = applyResult;

        private readonly bool _applyResult;
        public int ApplyCount { get; private set; }
        public bool Throw { get; set; }

        public override bool CanHandle(IIntegrationEvent e) => true;

        protected override Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken ct)
        {
            ApplyCount++;
            if (Throw) throw new InvalidOperationException("boom");
            return Task.FromResult(_applyResult);
        }
    }

    [Fact]
    public async Task Same_event_processed_once()
    {
        await using var db = NewContext();
        var handler = new CountingHandler(db, new FixedClock());
        var e = Event(Guid.NewGuid());

        await handler.HandleAsync(e);
        await handler.HandleAsync(e);

        Assert.Equal(1, handler.ApplyCount);
        Assert.Equal(1, await db.Set<InboxMessage>().CountAsync());
    }

    [Fact]
    public async Task Same_event_different_handlers_both_run()
    {
        await using var db = NewContext();
        var e = Event(Guid.NewGuid());
        var a = new NamedHandler(db, new FixedClock(), "A");
        var b = new NamedHandler(db, new FixedClock(), "B");

        await a.HandleAsync(e);
        await b.HandleAsync(e);

        Assert.Equal(1, a.ApplyCount);
        Assert.Equal(1, b.ApplyCount);
        Assert.Equal(2, await db.Set<InboxMessage>().CountAsync());
    }

    [Fact]
    public async Task Apply_returns_false_writes_no_inbox_row()
    {
        await using var db = NewContext();
        var handler = new CountingHandler(db, new FixedClock(), applyResult: false);

        await handler.HandleAsync(Event(Guid.NewGuid()));

        Assert.Equal(0, await db.Set<InboxMessage>().CountAsync());
    }

    [Fact]
    public async Task Apply_throws_writes_no_inbox_row()
    {
        await using var db = NewContext();
        var handler = new CountingHandler(db, new FixedClock()) { Throw = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(Event(Guid.NewGuid())));

        Assert.Equal(0, await db.Set<InboxMessage>().CountAsync());
    }

    private sealed class NamedHandler : IdempotentIntegrationEventHandler
    {
        public NamedHandler(ModuleDbContext db, IClock clock, string name) : base(db, clock) => _name = name;
        private readonly string _name;
        public int ApplyCount { get; private set; }
        protected override string HandlerName => _name;
        public override bool CanHandle(IIntegrationEvent e) => true;
        protected override Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken ct)
        {
            ApplyCount++;
            return Task.FromResult(true);
        }
    }
}
