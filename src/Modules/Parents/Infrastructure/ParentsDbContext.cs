using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Parents.Infrastructure;

public sealed class ParentsDbContext : ModuleDbContext
{
    public const string SchemaName = "parents";

    public ParentsDbContext(
        DbContextOptions<ParentsDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Parents";
}
