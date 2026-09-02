using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Application;
using EgitimUssu.Shared.Infrastructure.Authentication;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Email;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Infrastructure.Push;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<PushOptions>(configuration.GetSection(PushOptions.SectionName));

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

        // Y4: Redis destekli dağıtık rate limiting + token blacklist (fail-open) + dayanıklı Redis erişimi.
        services.AddSingleton<ResilientRedisExecutor>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();
        services.AddSingleton<ITokenBlacklist, RedisTokenBlacklist>();
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();

        // K-01: E-posta sağlayıcısı konfigürasyonla seçilir; kod hiçbir sağlayıcıya bağlı değildir.
        services.AddSingleton<ISmtpClientFactory, MailKitSmtpClientFactory>();
        services.AddScoped<IEmailSender>(provider =>
        {
            var emailOptions = provider.GetRequiredService<IOptions<EmailOptions>>().Value;

            return string.Equals(emailOptions.Provider, EmailOptions.SmtpProvider, StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider)
                : ActivatorUtilities.CreateInstance<LoggingEmailSender>(provider);
        });

        // K-02: Push sağlayıcısı konfigürasyonla seçilir; uygulama kodu FCM'i bilmez.
        services.AddSingleton<IFcmAccessTokenProvider, GoogleFcmAccessTokenProvider>();
        services.AddHttpClient<FcmPushSender>();
        services.AddScoped<IPushSender>(provider =>
        {
            var pushOptions = provider.GetRequiredService<IOptions<PushOptions>>().Value;

            return string.Equals(pushOptions.Provider, PushOptions.FcmProvider, StringComparison.OrdinalIgnoreCase)
                ? provider.GetRequiredService<FcmPushSender>()
                : ActivatorUtilities.CreateInstance<LoggingPushSender>(provider);
        });

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
