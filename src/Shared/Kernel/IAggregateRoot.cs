namespace EgitimUssu.Shared.Kernel;

public interface IAggregateRoot
{
    IReadOnlyCollection<DomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
