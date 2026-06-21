using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Reviews.Infrastructure;

public sealed class ReviewsDbContext : ModuleDbContext
{
    public const string SchemaName = "reviews";

    public ReviewsDbContext(
        DbContextOptions<ReviewsDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Reviews";
}
