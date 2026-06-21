using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure;

public sealed class GuidIdGenerator : IIdGenerator
{
    public Guid New() => Guid.NewGuid();
}
