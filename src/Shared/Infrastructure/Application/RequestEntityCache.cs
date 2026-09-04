using EgitimUssu.Shared.Application;

namespace EgitimUssu.Shared.Infrastructure.Application;

/// <summary>
/// <see cref="IRequestEntityCache"/>'in istek-kapsamlı (scoped) implementasyonu.
/// Anahtar (tip, key) çiftidir; aynı anahtar farklı tiple istendiğinde eşleşme olmaz.
/// </summary>
internal sealed class RequestEntityCache : IRequestEntityCache
{
    private readonly Dictionary<(Type Type, object Key), object> _entries = [];

    public void Set<T>(object key, T entity) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entity);

        _entries[(typeof(T), key)] = entity;
    }

    public bool TryGet<T>(object key, out T? entity) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_entries.TryGetValue((typeof(T), key), out var found) && found is T typed)
        {
            entity = typed;
            return true;
        }

        entity = null;
        return false;
    }
}
