using Microsoft.AspNetCore.Http;

namespace EgitimUssu.Shared.Infrastructure.Middleware;

/// <summary>
/// A-17: Her yanıta tarayıcı sertleştirme başlıklarını ekler.
/// <para>
/// Bugünkü istemci mobil olduğu için bu başlıkların çoğu etkisizdir; ama API'nin döndürdüğü JSON
/// bir tarayıcıda açıldığında (OpenAPI belgesi, hata sayfası, ileride Angular istemci) MIME
/// sniffing ve çerçeveleme gerçek saldırı yüzeyi hâline gelir. Başlıkları sonradan eklemek,
/// yerleşmiş bir web istemcisiyle birlikte hata ayıklamalı bir işe dönüşür.
/// </para>
/// <para>
/// CSP bilinçli olarak <b>dar</b>: API yalnız veri döndürür, hiçbir kaynağı gömmez — dolayısıyla
/// her şeyi reddeden bir politika doğru olandır. Bir gün bu host'tan HTML sunulursa politika
/// o sayfayla birlikte genişletilmelidir.
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>İsteği işler ve yanıt başlamadan önce güvenlik başlıklarını yazar.</summary>
    /// <param name="context">İstek bağlamı.</param>
    /// <returns>İşlem görevi.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        // Başlıklar yanıt gövdesi yazılmaya başlamadan önce eklenmeli; OnStarting bunu garanti eder
        // (aksi hâlde akıtılan bir yanıtta "response has already started" hatası alınır).
        context.Response.OnStarting(static state =>
        {
            var headers = ((HttpContext)state).Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";

            // Sunucu/çatı sürümünü sızdıran başlıkları kaldır — sürüme özgü açıkların taranmasını kolaylaştırır.
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            return Task.CompletedTask;
        }, context);

        return next(context);
    }
}
