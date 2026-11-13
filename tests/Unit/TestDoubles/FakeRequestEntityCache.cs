using EgitimUssu.Shared.Application;

namespace EgitimUssu.Tests.Unit.TestDoubles;

/// <summary>
/// <see cref="IRequestEntityCache"/>'in test karşılığı: tek bir istek kapsamını temsil eder.
/// Gerçek implementasyonla aynı (tip, anahtar) eşleştirmesini yapar.
/// </summary>
public sealed class FakeRequestEntityCache : IRequestEntityCache
{
    private readonly Dictionary<(Type Type, object Key), object> _entries = [];

    public void Set<T>(object key, T entity) where T : class => _entries[(typeof(T), key)] = entity;

    public bool TryGet<T>(object key, out T? entity) where T : class
    {
        if (_entries.TryGetValue((typeof(T), key), out var found) && found is T typed)
        {
            entity = typed;
            return true;
        }

        entity = null;
        return false;
    }
}
