using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Matching.Infrastructure;

public sealed class MatchingDbContext : ModuleDbContext
{
    public const string SchemaName = "matching";

    public MatchingDbContext(
        DbContextOptions<MatchingDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Matching";
}
