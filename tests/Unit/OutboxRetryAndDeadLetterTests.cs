using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// K5: Zehirli mesaj sırayı bloklamamalı; başarısız mesaj retry/backoff almalı ve
/// azami deneme aşılınca dead-letter'a taşınmalı.
/// </summary>
public sealed class OutboxRetryAndDeadLetterTests
{
    private const string PoisonPayload = "POISON";
    private const string GoodPayload = "GOOD";

    [Fact]
    public async Task Poison_Message_Does_Not_Block_Sibling_And_Gets_Retry_Scheduled()
    {
        var now = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new OutboxOptions { MaxRetryCount = 5, RetryBackoffBaseSeconds = 30 };
        var (store, provider, poisonId, goodId) = await BuildStoreWithMessagesAsync(options, now);

        var published = await store.ProcessPendingAsync(PublishFailingOnPoison);

        Assert.Equal(1, published); // yalnız GOOD yayınlandı

        var good = await LoadAsync(provider, goodId);
        var poison = await LoadAsync(provider, poisonId);

        Assert.NotNull(good.ProcessedOnUtc); // zehirli mesaja rağmen işlendi
        Assert.Null(poison.ProcessedOnUtc);
        Assert.Null(poison.DeadLetteredOnUtc);
        Assert.Equal(1, poison.RetryCount);
        Assert.NotNull(poison.NextAttemptUtc);
        Assert.True(poison.NextAttemptUtc > now); // backoff geleceğe planlandı
        Assert.NotNull(poison.Error);
    }

    [Fact]
    public async Task Message_Is_Dead_Lettered_After_Max_Retries()
    {
        var now = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new OutboxOptions { MaxRetryCount = 1 }; // ilk başarısızlıkta dead-letter
        var (store, provider, poisonId, goodId) = await BuildStoreWithMessagesAsync(options, now);

        var published = await store.ProcessPendingAsync(PublishFailingOnPoison);

        Assert.Equal(1, published);

        var poison = await LoadAsync(provider, poisonId);
        Assert.NotNull(poison.DeadLetteredOnUtc); // kuyruktan çıkarıldı
        Assert.Null(poison.ProcessedOnUtc);
        Assert.Equal(1, poison.RetryCount);
        Assert.Null(poison.NextAttemptUtc); // dead-letter'da backoff planlanmaz

        // Dead-letter'lı mesaj bir daha claim edilmez.
        var republished = await store.ProcessPendingAsync(PublishFailingOnPoison);
        Assert.Equal(0, republished);
    }

    private static Task PublishFailingOnPoison(OutboxBatchItem item, CancellationToken _)
        => item.Payload == PoisonPayload
            ? throw new InvalidOperationException("handler patladı")
            : Task.CompletedTask;

    private static async Task<(EfOutboxStore Store, IServiceProvider Provider, Guid PoisonId, Guid GoodId)>
        BuildStoreWithMessagesAsync(OutboxOptions options, DateTime now)
    {
        var databaseName = $"outbox-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventMapper, JsonDomainEventMapper>();
        services.AddDbContext<TestOutboxDbContext>(builder => builder.UseInMemoryDatabase(databaseName));
        services.AddSingleton(new ModuleDbContextDescriptor("Test", "test", typeof(TestOutboxDbContext)));
        var provider = services.BuildServiceProvider();

        var poisonId = Guid.NewGuid();
        var goodId = Guid.NewGuid();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestOutboxDbContext>();
            db.OutboxMessages.Add(NewMessage(poisonId, PoisonPayload, now));
            db.OutboxMessages.Add(NewMessage(goodId, GoodPayload, now.AddSeconds(1)));
            await db.SaveChangesAsync();
        }

        var store = new EfOutboxStore(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            new FixedClock(now),
            NullLogger<EfOutboxStore>.Instance);

        return (store, provider, poisonId, goodId);
    }

    private static OutboxMessage NewMessage(Guid id, string payload, DateTime occurredOn)
        => new()
        {
            Id = id,
            Module = "Test",
            Type = "TestEvent",
            Payload = payload,
            OccurredOnUtc = occurredOn
        };

    private static async Task<OutboxMessage> LoadAsync(IServiceProvider provider, Guid id)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestOutboxDbContext>();
        return await db.OutboxMessages.SingleAsync(message => message.Id == id);
    }

    private sealed class TestOutboxDbContext(DbContextOptions<TestOutboxDbContext> options, IDomainEventMapper mapper)
        : ModuleDbContext(options, mapper)
    {
        protected override string Schema => "test";

        protected override string ModuleName => "Test";
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
