using EgitimUssu.Shared.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

public sealed class OutboxDispatcher(
    IOutboxProcessor outboxProcessor,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.DispatchEnabled)
        {
            // K1: Kapalıyken uyarı seviyesinde logla — modüller-arası tüm event akışı ölü demektir,
            // bu durumun startup'ta fark edilmeden geçmemesi için (Information değil Warning).
            logger.LogWarning(
                "Outbox dispatcher DISABLED via configuration (Outbox:DispatchEnabled=false). " +
                "Integration events will accumulate in outbox_messages and never be published.");
            return;
        }

        logger.LogInformation(
            "Outbox dispatcher enabled; polling every {PollIntervalSeconds}s (batch size {BatchSize}).",
            options.Value.PollIntervalSeconds,
            options.Value.BatchSize);

        // A-09: Budama poll döngüsünden ayrı, seyrek bir ritimde koşar — her 15 saniyede bir DELETE
        // çalıştırmanın anlamı yok. İlk tur açılıştan hemen sonra yapılır ki uzun süredir birikmiş
        // bir kuyruk ilk fırsatta küçülsün.
        var nextPurgeUtc = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await outboxProcessor.DispatchPendingAsync(stoppingToken);

                if (DateTime.UtcNow >= nextPurgeUtc)
                {
                    await PurgeAsync(stoppingToken);
                    nextPurgeUtc = DateTime.UtcNow.AddMinutes(options.Value.PurgeIntervalMinutes);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox dispatch cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task PurgeAsync(CancellationToken stoppingToken)
    {
        var retention = TimeSpan.FromDays(options.Value.RetentionDays);
        if (retention <= TimeSpan.Zero)
        {
            return;
        }

        var deleted = await outboxProcessor.PurgeProcessedAsync(retention, stoppingToken);
        if (deleted > 0)
        {
            logger.LogInformation(
                "Outbox/inbox retention removed {DeletedCount} processed row(s) older than {RetentionDays} day(s).",
                deleted,
                options.Value.RetentionDays);
        }
    }
}
