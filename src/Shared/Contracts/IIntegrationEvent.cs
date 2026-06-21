namespace EgitimUssu.Shared.Contracts;

public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }

    string Name { get; }

    string SourceModule { get; }
}
