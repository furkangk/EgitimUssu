namespace EgitimUssu.Shared.Application;

/// <summary>
/// O2: Bir istek kapsamında authorizer'ın yüklediği varlığı handler'a taşır (çift DB sorgusunu önler).
/// Kayıt <b>scoped</b>'dır; bir isteğin verisi başka bir isteğe sızmaz.
/// </summary>
public interface IRequestEntityCache
{
    /// <summary>Varlığı bu istek kapsamında <paramref name="key"/> altında saklar.</summary>
    void Set<T>(object key, T entity) where T : class;

    /// <summary>Aynı istek kapsamında daha önce saklanmış varlığı döndürür.</summary>
    bool TryGet<T>(object key, out T? entity) where T : class;
}

/// <summary>
/// O2: Authorizer ile handler'ın aynı varlığı tek sorguda paylaşması için ortak yardımcı.
/// Hangisi önce koşarsa yükler; ikincisi istek kapsamından okur.
/// </summary>
public static class RequestEntityCacheExtensions
{
    /// <summary>
    /// Varlık bu istek kapsamında zaten yüklendiyse onu döndürür, aksi hâlde
    /// <paramref name="load"/> ile yükleyip saklar. <b>Bulunamayan varlık saklanmaz</b> —
    /// yokluk kalıcı bir cevap değildir, sonraki çağrı yeniden dener.
    /// </summary>
    public static async Task<T?> GetOrLoadAsync<T>(
        this IRequestEntityCache cache,
        object key,
        Func<CancellationToken, Task<T?>> load,
        CancellationToken cancellationToken)
        where T : class
    {
        if (cache.TryGet<T>(key, out var cached))
        {
            return cached;
        }

        var entity = await load(cancellationToken);
        if (entity is not null)
        {
            cache.Set(key, entity);
        }

        return entity;
    }
}
