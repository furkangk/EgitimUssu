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
            logger.LogInformation("Outbox dispatcher disabled via configuration.");
            return;
        }

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
