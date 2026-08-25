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
}

public sealed class NoOpDomainEventMapper : IDomainEventMapper
{
    public IReadOnlyCollection<IIntegrationEvent> Map(string sourceModule, DomainEvent domainEvent)
        => Array.Empty<IIntegrationEvent>();
}
