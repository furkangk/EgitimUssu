using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Application;
using EgitimUssu.Shared.Infrastructure.Authentication;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Email;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Infrastructure.Observability;
using EgitimUssu.Shared.Infrastructure.Push;
using EgitimUssu.Shared.Infrastructure.Storage;
using EgitimUssu.Shared.Kernel.Storage;
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
        // E-07: Saklama süresi dolan teknik kayıtları (işlenmiş outbox/inbox) temizler.
        services.AddScoped<Persistence.IDataRetentionProcessor, Persistence.DataRetentionProcessor>();
        services.AddHostedService<Persistence.DataRetentionService>();
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<PushOptions>(configuration.GetSection(PushOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, GuidIdGenerator>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<IRequestEntityCache, RequestEntityCache>();
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        services.AddSingleton<IDomainEventMapper, JsonDomainEventMapper>();
        services.AddSingleton<IOutboxStore, EfOutboxStore>();
        // A-07: Arka plan işleri tur başına kira alır; ikinci bir uygulama örneği aynı turu koşamaz.
        services.AddSingleton<IJobLeaseStore, JobLeaseStore>();
        services.AddSingleton<IOutboxProcessor, OutboxProcessor>();
        services.AddSingleton<IEventBus, DispatchingEventBus>();
        services.AddHostedService<OutboxDispatcher>();

        // A-01: Kod içinde "localhost:6379" yedeği yok. Boş dize verilirse bağlantı kurulamaz ve
        // korumalar fail-open'a düşer — bunun üretimde sessizce olmaması RedisOptionsGuard'ın işidir.
        var redisConfiguration = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()?.Configuration
            ?? string.Empty;
        services.AddSingleton<IRedisConnectionFactory>(_ => new LazyRedisConnectionFactory(redisConfiguration));

        // Y4: Redis destekli dağıtık rate limiting + token blacklist (fail-open) + dayanıklı Redis erişimi.
        services.AddSingleton<ResilientRedisExecutor>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();
        services.AddSingleton<ITokenBlacklist, RedisTokenBlacklist>();
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();

        // P15 / N-20: Davet ve katılım kodları kriptografik rastgelelikle üretilir; kod tahmini
        // kullanıcı başına sınırlanır (Redis, fail-open).
        services.AddSingleton<ISecureCodeGenerator, SecureCodeGenerator>();
        services.AddSingleton<IInviteClaimAttemptLimiter, InviteClaimAttemptLimiter>();

        // K-01: E-posta sağlayıcısı konfigürasyonla seçilir; kod hiçbir sağlayıcıya bağlı değildir.
        services.AddSingleton<ISmtpClientFactory, MailKitSmtpClientFactory>();
        services.AddScoped<IEmailSender>(provider =>
        {
            var emailOptions = provider.GetRequiredService<IOptions<EmailOptions>>().Value;

            IEmailSender sender =
                string.Equals(emailOptions.Provider, EmailOptions.SmtpProvider, StringComparison.OrdinalIgnoreCase)
                    ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider)
                    : ActivatorUtilities.CreateInstance<LoggingEmailSender>(provider);

            // C-04: Ölçüm kompozisyon noktasında sarmalanır — hangi sağlayıcı seçilirse seçilsin çalışır.
            return new MeteredEmailSender(sender, provider.GetRequiredService<EgitimUssuMetrics>());
        });

        // A-04: E-posta artık istek yolunda gönderilmez; iş yazımıyla aynı transaction'da kuyruğa
        // girer, arka planda retry/backoff/dead-letter ile gönderilir (push'un çoktan aldığı tedavi).
        services.AddSingleton<IEmailQueue, EmailQueue>();
        services.AddHostedService<OutgoingEmailDispatcher>();

        // K-02: Push sağlayıcısı konfigürasyonla seçilir; uygulama kodu FCM'i bilmez.
        services.AddSingleton<IFcmAccessTokenProvider, GoogleFcmAccessTokenProvider>();
        services.AddHttpClient<FcmPushSender>();
        services.AddScoped<IPushSender>(provider =>
        {
            var pushOptions = provider.GetRequiredService<IOptions<PushOptions>>().Value;

            IPushSender sender =
                string.Equals(pushOptions.Provider, PushOptions.FcmProvider, StringComparison.OrdinalIgnoreCase)
                    ? provider.GetRequiredService<FcmPushSender>()
                    : ActivatorUtilities.CreateInstance<LoggingPushSender>(provider);

            // C-04: Ölçüm kompozisyon noktasında sarmalanır — hangi sağlayıcı seçilirse seçilsin çalışır.
            return new MeteredPushSender(sender, provider.GetRequiredService<EgitimUssuMetrics>());
        });

        // K-03: Dosya depolama sağlayıcısı konfigürasyonla seçilir; modüller S3'ü bilmez.
        services.AddSingleton<IFileStorage>(provider =>
        {
            var storageOptions = provider.GetRequiredService<IOptions<StorageOptions>>().Value;

            return string.Equals(storageOptions.Provider, StorageOptions.S3Provider, StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<S3FileStorage>(provider)
                : ActivatorUtilities.CreateInstance<LocalFileStorage>(provider);
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

            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema);

                // A-18: Yönetilen veritabanında (Render/Upstash) kısa kopmalar rutindir; retry olmadan
                // her biri doğrudan 500'e dönüşür. Komut zaman aşımı da açıkça verilir — varsayılan 30 sn,
                // ücretsiz plandaki soğuk bir örnekte tarama sorgularına yetmiyor.
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                npgsql.CommandTimeout(60);
            });
        });

        // A-18: `AddDbContext<TContext>` bu tipi zaten scoped kaydeder; ikinci bir `AddScoped` kaydı
        // EF'in kendi kaydını gölgeliyordu (son kayıt kazanır). Kaldırıldı.
        services.AddSingleton(new ModuleDbContextDescriptor(moduleName, schema, typeof(TContext)));

        return services;
    }
}
