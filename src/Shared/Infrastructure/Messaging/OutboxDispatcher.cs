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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await outboxProcessor.DispatchPendingAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox dispatch cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
        }
    }
}
