namespace EgitimUssu.Shared.Kernel;

public interface IClock
{
    DateTime UtcNow { get; }
}
