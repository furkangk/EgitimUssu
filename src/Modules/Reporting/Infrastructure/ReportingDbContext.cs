using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

public sealed class ReportingDbContext : ModuleDbContext
{
    public const string SchemaName = "reporting";

    public ReportingDbContext(
        DbContextOptions<ReportingDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Reporting";
}
