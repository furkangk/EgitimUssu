using System.Collections.Generic;

namespace EgitimUssu.Shared.Kernel;

public sealed record Error(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
