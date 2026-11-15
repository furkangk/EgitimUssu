using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EgitimUssu.Tests.Unit;

public sealed class DispatchingEventBusIsolationTests
{
    [Fact]
    public async Task Failure_Does_Not_Starve_Siblings_Or_Flush_Failed_Changes_And_Retry_Uses_Inbox()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        var calls = new int[4];
        services.AddScoped(_ => new TestModuleDbContext(new DbContextOptionsBuilder<ModuleDbContext>()
            .UseInMemoryDatabase(dbName).Options));
        for (var index = 0; index < 4; index++)
        {
            var slot = index;
            services.AddScoped<IIntegrationEventHandler>(s => new Probe(s.GetRequiredService<TestModuleDbContext>(), slot, calls));
        }
        using var provider = services.BuildServiceProvider();
        var bus = new DispatchingEventBus(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<DispatchingEventBus>.Instance);
        var envelope = new IntegrationEvent(Guid.NewGuid(), DateTime.UnixEpoch, "Isolation", "Test", "{}");

        for (var retry = 0; retry < 2; retry++)
        {
            var failure = await Assert.ThrowsAsync<AggregateException>(() => bus.PublishAsync(envelope));
            var handlerFailure = Assert.IsType<IntegrationEventHandlerException>(Assert.Single(failure.InnerExceptions));
            Assert.Contains(nameof(Probe), handlerFailure.HandlerName);
        }

        Assert.Equal(new[] { 1, 2, 1, 1 }, calls);
        using var read = provider.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<TestModuleDbContext>();
        Assert.Equal(3, await db.Widgets.CountAsync());
        Assert.Equal(3, await db.InboxMessages.CountAsync());
    }

    [Fact]
    public async Task Unrequested_Cancellation_Is_A_Handler_Failure_But_Requested_Cancellation_Stops_Dispatch()
    {
        foreach (var cancel in new[] { false, true })
        {
            using var cts = new CancellationTokenSource();
            var calls = 0;
            var services = new ServiceCollection();
            services.AddSingleton<IIntegrationEventHandler>(new Callback(() =>
            {
                if (cancel) cts.Cancel();
                throw new OperationCanceledException();
            }));
            services.AddSingleton<IIntegrationEventHandler>(new Callback(() => calls++));
            using var provider = services.BuildServiceProvider();
            var bus = new DispatchingEventBus(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<DispatchingEventBus>.Instance);
            var envelope = new IntegrationEvent(Guid.NewGuid(), DateTime.UnixEpoch, "Isolation", "Test", "{}");
            if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bus.PublishAsync(envelope, cts.Token));
            else await Assert.ThrowsAsync<AggregateException>(() => bus.PublishAsync(envelope, cts.Token));
            Assert.Equal(cancel ? 0 : 1, calls);
        }
    }

    [Fact]
    public void Production_Handlers_Must_Not_Return_Ambiguous_False()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "EgitimUssu.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var offenders = Directory.GetFiles(Path.Combine(root.FullName, "src", "Modules"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return (source.Contains("ApplyAsync(") || source.Contains("ProjectAsync("))
                    && System.Text.RegularExpressions.Regex.IsMatch(source, @"return\s+false\s*;|Task\.FromResult\(false\)");
            });
        Assert.Empty(offenders);
    }

    private sealed class Clock : IClock { public DateTime UtcNow => DateTime.UnixEpoch; }
    private sealed class Probe(TestModuleDbContext db, int slot, int[] calls) : IdempotentIntegrationEventHandler(db, new Clock())
    {
        protected override string HandlerName => $"Probe{slot}";
        public override bool CanHandle(IIntegrationEvent e) => true;
        protected override Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
        {
            calls[slot]++;
            db.Widgets.Add(new Widget { Id = Guid.NewGuid() });
            if (slot == 1) throw new InvalidOperationException("failed stage");
            return Task.FromResult(true);
        }
    }
    private sealed class Callback(Action callback) : IIntegrationEventHandler
    {
        public bool CanHandle(IIntegrationEvent e) => true;
        public Task HandleAsync(IIntegrationEvent e, CancellationToken cancellationToken = default)
        {
            callback();
            return Task.CompletedTask;
        }
    }
}
