using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

public sealed class ProgressTrackingDbContext : ModuleDbContext
{
    public const string SchemaName = "progress_tracking";

    public ProgressTrackingDbContext(
        DbContextOptions<ProgressTrackingDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    protected override string Schema => SchemaName;

    protected override string ModuleName => "ProgressTracking";
}
