using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Shared.Infrastructure.Persistence;

public sealed class EfOutboxStore(IServiceScopeFactory scopeFactory) : IOutboxStore
{
    public async Task<IReadOnlyCollection<OutboxBatchItem>> FetchPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var descriptors = scope.ServiceProvider.GetServices<ModuleDbContextDescriptor>().ToArray();
        var items = new List<OutboxBatchItem>();

        foreach (var descriptor in descriptors)
        {
            var dbContext = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(descriptor.DbContextType);
            var rows = await dbContext.OutboxMessages
                .AsNoTracking()
                .Where(message => message.ProcessedOnUtc == null)
                .OrderBy(message => message.OccurredOnUtc)
                .Take(batchSize)
                .Select(message => new OutboxBatchItem(
                    descriptor.DbContextType,
                    message.Id,
                    message.Module,
                    message.Type,
                    message.Payload,
                    message.OccurredOnUtc))
                .ToArrayAsync(cancellationToken);

            items.AddRange(rows);
        }

        return items
            .OrderBy(item => item.OccurredOnUtc)
            .Take(batchSize)
            .ToArray();
    }

    public async Task MarkProcessedAsync(
        IReadOnlyCollection<OutboxBatchItem> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var groups = items.GroupBy(item => item.DbContextType);

        foreach (var group in groups)
        {
            var dbContext = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(group.Key);
            var ids = group.Select(item => item.MessageId).ToArray();
            var messages = await dbContext.OutboxMessages
                .Where(message => ids.Contains(message.Id))
                .ToArrayAsync(cancellationToken);

            foreach (var message in messages)
            {
                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
