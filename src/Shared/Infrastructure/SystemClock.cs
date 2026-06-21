using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
