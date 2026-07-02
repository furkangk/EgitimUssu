using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Persistence;

public sealed class EfOutboxStore(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    IClock clock,
    ILogger<EfOutboxStore> logger) : IOutboxStore
{
    private const int ErrorMaxLength = 4000;

    public async Task<int> ProcessPendingAsync(
        Func<OutboxBatchItem, CancellationToken, Task> publishAsync,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        using var scope = scopeFactory.CreateScope();
        var descriptors = scope.ServiceProvider.GetServices<ModuleDbContextDescriptor>().ToArray();
        var processed = 0;

        foreach (var descriptor in descriptors)
        {
            // K4: Bir modülün tablosu eksik/erişilemez olsa bile (ör. migration drift) tüm outbox
            // dispatch'inin kalıcı durmaması için her context hatası izole edilir ve loglanır.
            try
            {
                var dbContext = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(descriptor.DbContextType);
                processed += await ProcessContextAsync(dbContext, descriptor, publishAsync, settings, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Outbox işleme {Module} modülü için başarısız oldu; diğer modüllerle devam ediliyor.",
                    descriptor.ModuleName);
            }
        }

        return processed;
    }

    private async Task<int> ProcessContextAsync(
        ModuleDbContext dbContext,
        ModuleDbContextDescriptor descriptor,
        Func<OutboxBatchItem, CancellationToken, Task> publishAsync,
        OutboxOptions settings,
        CancellationToken cancellationToken)
    {
        var messages = await ClaimAsync(dbContext, descriptor, settings, cancellationToken);
        if (messages.Count == 0)
        {
            return 0;
        }

        var processed = 0;
        foreach (var message in messages)
        {
            var item = new OutboxBatchItem(
                descriptor.DbContextType,
                message.Id,
                message.Module,
                message.Type,
                message.Payload,
                message.OccurredOnUtc);

            try
            {
                await publishAsync(item, cancellationToken);
                message.ProcessedOnUtc = clock.UtcNow;
                message.Error = null;
                processed++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                RecordFailure(message, exception, settings);
            }
        }

        // K5: Tüm mesaj sonuçları (başarı + başarısızlık) tek SaveChanges ile kalıcılaştırılır;
        // zehirli bir mesaj batch'teki diğerlerinin işaretlenmesini engellemez.
        await dbContext.SaveChangesAsync(cancellationToken);
        return processed;
    }

    private void RecordFailure(OutboxMessage message, Exception exception, OutboxOptions settings)
    {
        message.RetryCount++;
        message.Error = Truncate(exception.Message, ErrorMaxLength);

        if (message.RetryCount >= settings.MaxRetryCount)
        {
            message.DeadLetteredOnUtc = clock.UtcNow;
            logger.LogError(
                exception,
                "Outbox mesajı {MessageId} ({Type}) {RetryCount} denemeden sonra dead-letter'a taşındı.",
                message.Id,
                message.Type,
                message.RetryCount);
            return;
        }

        var backoffSeconds = ComputeBackoffSeconds(settings, message.RetryCount);
        message.NextAttemptUtc = clock.UtcNow.AddSeconds(backoffSeconds);
        logger.LogWarning(
            exception,
            "Outbox mesajı {MessageId} ({Type}) işlenemedi (deneme {RetryCount}); {BackoffSeconds}s sonra yeniden denenecek.",
            message.Id,
            message.Type,
            message.RetryCount,
            backoffSeconds);
    }

    private static int ComputeBackoffSeconds(OutboxOptions settings, int retryCount)
    {
        // Üstel backoff: Base × 2^(retryCount-1), MaxBackoff ile sınırlı. Taşmaya karşı long aritmetiği.
        var exponent = Math.Min(retryCount - 1, 30);
        var delay = (long)settings.RetryBackoffBaseSeconds * (1L << exponent);
        return (int)Math.Min(delay, settings.MaxBackoffSeconds);
    }

    /// <summary>
    /// Uygun mesajları claim eder. Npgsql'de <c>FOR UPDATE SKIP LOCKED</c> + lease ile çoklu-instance güvenli;
    /// InMemory (test/dev) sağlayıcıda kilit desteği olmadığından basit sıralı seçim yapılır.
    /// </summary>
    private async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        ModuleDbContext dbContext,
        ModuleDbContextDescriptor descriptor,
        OutboxOptions settings,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        if (dbContext.Database.IsNpgsql())
        {
            var leaseUntil = now.AddSeconds(settings.ClaimLeaseSeconds);
            var table = $"\"{descriptor.Schema}\".\"outbox_messages\"";
            var claimSql =
                $"UPDATE {table} SET \"NextAttemptUtc\" = {{0}} WHERE \"Id\" IN (" +
                $"SELECT \"Id\" FROM {table} " +
                "WHERE \"ProcessedOnUtc\" IS NULL AND \"DeadLetteredOnUtc\" IS NULL " +
                "AND (\"NextAttemptUtc\" IS NULL OR \"NextAttemptUtc\" <= {1}) " +
                "ORDER BY \"OccurredOnUtc\" LIMIT {2} FOR UPDATE SKIP LOCKED) RETURNING \"Id\"";

            var claimedIds = await dbContext.Database
                .SqlQueryRaw<Guid>(claimSql, leaseUntil, now, settings.BatchSize)
                .ToListAsync(cancellationToken);

            if (claimedIds.Count == 0)
            {
                return Array.Empty<OutboxMessage>();
            }

            return await dbContext.OutboxMessages
                .Where(message => claimedIds.Contains(message.Id))
                .OrderBy(message => message.OccurredOnUtc)
                .ToListAsync(cancellationToken);
        }

        return await dbContext.OutboxMessages
            .Where(message =>
                message.ProcessedOnUtc == null
                && message.DeadLetteredOnUtc == null
                && (message.NextAttemptUtc == null || message.NextAttemptUtc <= now))
            .OrderBy(message => message.OccurredOnUtc)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
