namespace EgitimUssu.Shared.Infrastructure.Messaging;

public interface IOutboxProcessor
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default);
}
