using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Http;

namespace EgitimUssu.Shared.Infrastructure.Middleware;

/// <summary>
/// C-10: Her <c>/api/*</c> yanıtına <c>X-Api-Version</c> başlığını ekler.
/// <para>
/// Sürüm iki kaynaktan çözülür ve sıra önemlidir:
/// <list type="number">
///   <item>ucun <see cref="ApiVersionMetadata"/> etiketi (modül grubundan gelir) — <b>kazanır</b>;</item>
///   <item>etiket yoksa yol <c>/api</c> ile başlıyorsa <see cref="ApiVersions.Current"/>.</item>
/// </list>
/// İkinci kural güvenlik ağıdır: <c>CreateModuleGroup</c> kullanmayan bir uç (ör. host'un kendi
/// <c>/api/meta/version</c>'ı) unutulsa bile sürümsüz yanıt dönmez.
/// </para>
/// <para>
/// Başlık <b>sonraki middleware'den önce</b> yazılır — 401/403/429/500 gibi erken biten yanıtlar da
/// sözleşme sürümünü taşımalıdır; istemci hata gördüğünde de hangi sürüme baktığını bilmelidir.
/// </para>
/// <para>
/// <c>/health/*</c> ve <c>/openapi/*</c> sürümlenmez: bunlar altyapı uçlarıdır, yayınlanan
/// sözleşmenin parçası değildir.
/// </para>
/// </summary>
public sealed class ApiVersionHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Api-Version";

    private static readonly PathString ApiPrefix = new("/api");

    public Task InvokeAsync(HttpContext context)
    {
        var version = ResolveVersion(context);
        if (version is not null)
        {
            context.Response.Headers[HeaderName] = version;
        }

        return next(context);
    }

    private static string? ResolveVersion(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<ApiVersionMetadata>();
        if (metadata is not null)
        {
            return metadata.Version;
        }

        return context.Request.Path.StartsWithSegments(ApiPrefix, StringComparison.OrdinalIgnoreCase)
            ? ApiVersions.Current
            : null;
    }
}
