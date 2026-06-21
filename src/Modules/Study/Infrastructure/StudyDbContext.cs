using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Study.Infrastructure;

public sealed class StudyDbContext : ModuleDbContext
{
    public const string SchemaName = "study";

    public StudyDbContext(
        DbContextOptions<StudyDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Study";
}
