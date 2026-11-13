using System.Diagnostics;
using System.Diagnostics.Metrics;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Application;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Observability;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// C-04 / P13 Task 9: Outbox asenkron bir hop'tur — isteğin trace'i mesajla birlikte taşınmazsa
/// zincir orada kopar ve "HTTP → command → EF → outbox → handler" tek trace'te görünmez.
/// Bu testler üç şeyi sabitler: (1) üreten isteğin `traceparent`'ı satıra yazılır,
/// (2) dispatch o trace'i **devam ettirir**, (3) trace yokken yeni bir kök açılır (çökmez).
/// </summary>
public sealed class OutboxTracePropagationTests
{
    [Fact]
    public async Task Outbox_Row_Stores_Producing_TraceParent()
    {
        using var recorder = new ActivityRecorder();
        using var producing = EgitimUssuActivitySource.Instance.StartActivity("test.request");
        Assert.NotNull(producing);

        await using var provider = BuildProvider();
        await AddAggregateAsync(provider);

        var message = await SingleMessageAsync(provider);
        Assert.Equal(producing.Id, message.TraceParent);
    }

    [Fact]
    public async Task Outbox_Row_TraceParent_Is_Null_Without_Activity()
    {
        // Dinleyici yok → `StartActivity` null döner → `Activity.Current` yok.
        // Tohumlama/arka plan işleri gerçekten böyle çalışır; satır null kalmalı, patlamamalı.
        Assert.Null(Activity.Current);

        await using var provider = BuildProvider();
        await AddAggregateAsync(provider);

        var message = await SingleMessageAsync(provider);
        Assert.Null(message.TraceParent);
    }

    [Fact]
    public async Task Dispatch_Span_Continues_Producing_Trace()
    {
        using var recorder = new ActivityRecorder();

        string traceParent;
        ActivityTraceId producingTraceId;
        using (var producing = EgitimUssuActivitySource.Instance.StartActivity("test.request"))
        {
            Assert.NotNull(producing);
            traceParent = producing.Id!;
            producingTraceId = producing.TraceId;
        }

        await using var provider = BuildProvider();
        var store = NewStore(provider);
        await SeedMessageAsync(provider, traceParent);

        var published = await store.ProcessPendingAsync((_, _) => Task.CompletedTask);
        Assert.Equal(1, published);

        // Dinleyici süreç genelidir: paralel koşan başka testlerin span'ları da düşer.
        // Filtre bu testin kendi trace'i — rastgele üretildiği için başka testle çakışmaz.
        var dispatch = Assert.Single(recorder.Named("outbox.dispatch", producingTraceId));
        Assert.Equal(ActivityKind.Consumer, dispatch.Kind);
    }

    [Fact]
    public async Task Dispatch_Span_Falls_Back_To_Poll_Span_When_TraceParent_Missing()
    {
        using var recorder = new ActivityRecorder();
        using var testRoot = EgitimUssuActivitySource.Instance.StartActivity("test.root");
        Assert.NotNull(testRoot);

        await using var provider = BuildProvider();
        var store = NewStore(provider);
        await SeedMessageAsync(provider, traceParent: null);

        var published = await store.ProcessPendingAsync((_, _) => Task.CompletedTask);
        Assert.Equal(1, published);

        // Bağlam yoksa mesaj yetim kalmaz: içinde bulunduğu turun altına düşer.
        var poll = Assert.Single(recorder.Named("outbox.poll", testRoot.TraceId));
        var dispatch = Assert.Single(recorder.Named("outbox.dispatch", testRoot.TraceId));
        Assert.Equal(poll.SpanId, dispatch.ParentSpanId);
    }

    [Fact]
    public async Task Dispatch_Span_Ignores_Malformed_TraceParent()
    {
        using var recorder = new ActivityRecorder();
        using var testRoot = EgitimUssuActivitySource.Instance.StartActivity("test.root");
        Assert.NotNull(testRoot);

        await using var provider = BuildProvider();
        var store = NewStore(provider);
        await SeedMessageAsync(provider, traceParent: "bu-gecerli-bir-traceparent-degil");

        // Bozuk değer mesajı kuyrukta kilitlememeli: span açılır, yayınlama başarılı olur.
        var published = await store.ProcessPendingAsync((_, _) => Task.CompletedTask);
        Assert.Equal(1, published);
        Assert.Single(recorder.Named("outbox.dispatch", testRoot.TraceId));
    }

    [Fact]
    public async Task Poll_Cycle_Groups_Its_Database_Work_Under_One_Span()
    {
        using var recorder = new ActivityRecorder();
        using var testRoot = EgitimUssuActivitySource.Instance.StartActivity("test.root");
        Assert.NotNull(testRoot);

        await using var provider = BuildProvider();
        var store = NewStore(provider);

        // Hiç mesaj yokken bile tur bir span üretmeli: claim/sayım sorguları aksi hâlde
        // ebeveynsiz kök trace olarak yayılıyor (Task 8 ölçümü: 1500 trace'in 1488'i).
        await store.ProcessPendingAsync((_, _) => Task.CompletedTask);

        Assert.Single(recorder.Named("outbox.poll", testRoot.TraceId));
    }

    [Fact]
    public async Task Handler_Invocation_Creates_Span_Per_Handler()
    {
        using var recorder = new ActivityRecorder();
        using var testRoot = EgitimUssuActivitySource.Instance.StartActivity("test.root");
        Assert.NotNull(testRoot);

        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationEventHandler, FirstHandler>();
        services.AddSingleton<IIntegrationEventHandler, SecondHandler>();
        await using var provider = services.BuildServiceProvider();

        var bus = new DispatchingEventBus(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DispatchingEventBus>.Instance);

        await bus.PublishAsync(NewIntegrationEvent());

        var handlerSpans = recorder.Named("handler", testRoot.TraceId).ToArray();
        Assert.Equal(2, handlerSpans.Length);
        Assert.Contains(handlerSpans, span => span.DisplayName.Contains(nameof(FirstHandler), StringComparison.Ordinal));
        Assert.Contains(handlerSpans, span => span.DisplayName.Contains(nameof(SecondHandler), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Command_Dispatch_Creates_Span()
    {
        using var recorder = new ActivityRecorder();

        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandler<TracedCommand, Result>, TracedCommandHandler>();
        await using var provider = services.BuildServiceProvider();

        var dispatcher = new CommandDispatcher(provider, NullLogger<CommandDispatcher>.Instance);
        var result = await dispatcher.Dispatch(new TracedCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Ad testin kendi tipinden geldiği için paralel koşan başka dispatch'lerle karışmaz.
        Assert.Single(recorder.Named($"command {nameof(TracedCommand)}"));
    }

    [Fact]
    public async Task Query_Dispatch_Creates_Span()
    {
        using var recorder = new ActivityRecorder();

        var services = new ServiceCollection();
        services.AddSingleton<IQueryHandler<TracedQuery, Result<int>>, TracedQueryHandler>();
        await using var provider = services.BuildServiceProvider();

        var dispatcher = new QueryDispatcher(provider, NullLogger<QueryDispatcher>.Instance);
        var result = await dispatcher.Dispatch(new TracedQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(recorder.Named($"query {nameof(TracedQuery)}"));
    }

    // ---------------------------------------------------------------- yardımcılar

    private static ServiceProvider BuildProvider()
    {
        // Ad lambda'nin ICINDE uretilirse her DbContext ornegi AYRI bir veritabani acar:
        // tohumlanan mesaj bir yerde, store'un okudugu tablo bambaska bir yerde kalir.
        var databaseName = $"trace-outbox-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventMapper, JsonDomainEventMapper>();
        services.AddDbContext<TraceOutboxDbContext>(builder => builder.UseInMemoryDatabase(databaseName));
        services.AddSingleton(new ModuleDbContextDescriptor("Test", "test", typeof(TraceOutboxDbContext)));
        services.AddMetrics();
        return services.BuildServiceProvider();
    }

    private static EfOutboxStore NewStore(IServiceProvider provider)
        => new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OutboxOptions()),
            new FixedClock(new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc)),
            new EgitimUssuMetrics(provider.GetRequiredService<IMeterFactory>()),
            NullLogger<EfOutboxStore>.Instance);

    private static async Task AddAggregateAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TraceOutboxDbContext>();
        db.Aggregates.Add(new TraceAggregate());
        await db.SaveChangesAsync();
    }

    private static async Task SeedMessageAsync(IServiceProvider provider, string? traceParent)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TraceOutboxDbContext>();
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Module = "Test",
            Type = "TraceTestEvent",
            Payload = "{}",
            OccurredOnUtc = new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc),
            TraceParent = traceParent
        });
        await db.SaveChangesAsync();
    }

    private static async Task<OutboxMessage> SingleMessageAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TraceOutboxDbContext>();
        return await db.OutboxMessages.SingleAsync();
    }

    private static IntegrationEvent NewIntegrationEvent()
        => new(
            Guid.NewGuid(),
            new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc),
            "TraceTestEvent",
            "Test",
            "{}");

    /// <summary>Test süresince <c>EgitimUssu</c> kaynağını dinler ve üretilen span'ları toplar.</summary>
    private sealed class ActivityRecorder : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly List<Activity> _activities = [];
        private readonly Lock _gate = new();

        public ActivityRecorder()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == EgitimUssuActivitySource.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    lock (_gate)
                    {
                        _activities.Add(activity);
                    }
                }
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public IEnumerable<Activity> Named(string prefix)
        {
            lock (_gate)
            {
                return _activities
                    .Where(activity => activity.DisplayName.StartsWith(prefix, StringComparison.Ordinal))
                    .ToArray();
            }
        }

        /// <summary>Yalnız verilen trace'e ait span'lar. Dinleyici süreç genelinde olduğu için şart.</summary>
        public IEnumerable<Activity> Named(string prefix, ActivityTraceId traceId)
            => Named(prefix).Where(activity => activity.TraceId == traceId);

        public void Dispose() => _listener.Dispose();
    }

    private sealed record TracedCommand : ICommand<Result>;

    private sealed record TracedQuery : IQuery<Result<int>>;

    private sealed class TracedCommandHandler : ICommandHandler<TracedCommand, Result>
    {
        public Task<Result> Handle(TracedCommand command, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success());
    }

    private sealed class TracedQueryHandler : IQueryHandler<TracedQuery, Result<int>>
    {
        public Task<Result<int>> Handle(TracedQuery query, CancellationToken cancellationToken)
            => Task.FromResult(Result<int>.Success(1));
    }

    private sealed class FirstHandler : IIntegrationEventHandler
    {
        public bool CanHandle(IIntegrationEvent integrationEvent) => true;

        public Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class SecondHandler : IIntegrationEventHandler
    {
        public bool CanHandle(IIntegrationEvent integrationEvent) => true;

        public Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class TraceAggregate : IAggregateRoot
    {
        private readonly List<DomainEvent> _events = [new TraceDomainEvent()];

        public Guid Id { get; set; } = Guid.NewGuid();

        public IReadOnlyCollection<DomainEvent> DomainEvents => _events;

        public void ClearDomainEvents() => _events.Clear();
    }

    private sealed record TraceDomainEvent : DomainEvent;

    private sealed class TraceOutboxDbContext(DbContextOptions<TraceOutboxDbContext> options, IDomainEventMapper mapper)
        : ModuleDbContext(options, mapper)
    {
        protected override string Schema => "test";

        protected override string ModuleName => "Test";

        public DbSet<TraceAggregate> Aggregates => Set<TraceAggregate>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TraceAggregate>(builder =>
            {
                builder.ToTable("trace_aggregates");
                builder.HasKey(aggregate => aggregate.Id);
                builder.Ignore(aggregate => aggregate.DomainEvents);
            });
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
