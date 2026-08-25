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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Widget>(builder =>
        {
            builder.ToTable("widgets");
            builder.HasKey(item => item.Id);
        });
    }
}

/// <summary>Testler için basit bir iş-yazımı entity'si (ApplyAsync staging sızıntısını doğrulamak için).</summary>
public sealed class Widget
{
    public Guid Id { get; set; }
}

public sealed class NoOpDomainEventMapper : IDomainEventMapper
{
    public IReadOnlyCollection<IIntegrationEvent> Map(string sourceModule, DomainEvent domainEvent)
        => Array.Empty<IIntegrationEvent>();
}
