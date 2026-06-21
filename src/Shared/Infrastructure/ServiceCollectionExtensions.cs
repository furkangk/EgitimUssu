using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Application;
using EgitimUssu.Shared.Infrastructure.Authentication;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Shared.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, GuidIdGenerator>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        services.AddSingleton<IDomainEventMapper, JsonDomainEventMapper>();
        services.AddSingleton<IOutboxStore, EfOutboxStore>();
        services.AddSingleton<IOutboxProcessor, OutboxProcessor>();
        services.AddSingleton<IEventBus, DispatchingEventBus>();
        services.AddHostedService<OutboxDispatcher>();

        var redisConfiguration = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()?.Configuration
            ?? "localhost:6379";
        services.AddSingleton<IRedisConnectionFactory>(_ => new LazyRedisConnectionFactory(redisConfiguration));

        return services;
    }

    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string moduleName,
        string schema)
        where TContext : ModuleDbContext
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
        var useInMemory = connectionString.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase);
        var inMemoryDatabaseName = useInMemory
            ? $"{connectionString["InMemory:".Length..]}-{schema}"
            : string.Empty;

        services.AddDbContext<TContext>((_, options) =>
        {
            if (useInMemory)
            {
                options.UseInMemoryDatabase(inMemoryDatabaseName);
                return;
            }

            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema));
        });

        services.AddScoped(typeof(TContext));
        services.AddSingleton(new ModuleDbContextDescriptor(moduleName, schema, typeof(TContext)));

        return services;
    }
}
