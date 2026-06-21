using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EgitimUssu.Shared.Infrastructure.Design;

public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    protected abstract string Schema { get; }

    public TContext CreateDbContext(string[] args)
    {
        var connectionString = args
            .FirstOrDefault(argument => argument.StartsWith("--connection=", StringComparison.OrdinalIgnoreCase))
            ?.Split("=", 2)[1]
            ?? Environment.GetEnvironmentVariable("EGITIMUSSU_POSTGRES")
            ?? "Host=localhost;Port=5432;Database=egitimussu;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", Schema));

        return (TContext)Activator.CreateInstance(
            typeof(TContext),
            optionsBuilder.Options,
            new NoOpDomainEventMapper())!;
    }
}
