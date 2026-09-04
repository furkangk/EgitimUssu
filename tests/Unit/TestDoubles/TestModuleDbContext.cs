using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit.TestDoubles;

public sealed class TestModuleDbContext : ModuleDbContext
{
    public TestModuleDbContext(DbContextOptions options)
        : base(options, new NoOpDomainEventMapper())
    {
    }

    protected override string Schema => "test";

    protected override string ModuleName => "Test";

    public DbSet<Widget> Widgets => Set<Widget>();

    public DbSet<TestCounterProjection> CounterProjections => Set<TestCounterProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Widget>(builder =>
        {
            builder.ToTable("widgets");
            builder.HasKey(item => item.Id);
        });

        modelBuilder.Entity<TestCounterProjection>(builder =>
        {
            builder.ToTable("counter_projections");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
        });
    }
}

/// <summary>Testler için basit bir iş-yazımı entity'si (ApplyAsync staging sızıntısını doğrulamak için).</summary>
public sealed class Widget
{
    public Guid Id { get; set; }
}

/// <summary>Testler için basit bir projeksiyon entity'si (<see cref="ProjectionEntity"/> davranışını doğrulamak için).</summary>
public sealed class TestCounterProjection : ProjectionEntity
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public int Count { get; private set; }

    public void Add(int amount) => Count += amount;
}

public sealed class NoOpDomainEventMapper : IDomainEventMapper
{
    public IReadOnlyCollection<IIntegrationEvent> Map(string sourceModule, DomainEvent domainEvent)
        => Array.Empty<IIntegrationEvent>();
}
