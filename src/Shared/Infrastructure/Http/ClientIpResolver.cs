using Microsoft.AspNetCore.Http;

namespace EgitimUssu.Shared.Infrastructure.Http;

/// <summary>
/// A-02: İstemci IP'sini ters-proxy zinciri altında güvenilir biçimde çözer.
/// <para>
/// Hem hız sınırı hem giriş denemesi kilidi aynı kimliğe dayanır; iki yerde ayrı ayrı yazılsaydı
/// biri düzeltilip diğeri sahte başlığa açık kalabilirdi. Tek kaynak burasıdır.
/// </para>
/// </summary>
public static class ClientIpResolver
{
    /// <summary>Bilinmeyen istemci için kullanılan sabit.</summary>
    public const string Unknown = "unknown";

    /// <summary>
    /// İstemci kimliğini <c>X-Forwarded-For</c>'un <b>sonundan</b> güvenilir proxy sayısı kadar
    /// geriden okur; başlık yoksa ya da zincir beklenenden kısaysa soket adresine düşer.
    /// </summary>
    /// <param name="context">İstek bağlamı; <c>null</c> ise <see cref="Unknown"/> döner.</param>
    /// <param name="trustedProxyHopCount">Önümüzdeki güvenilir proxy sayısı; <c>0</c> ise başlık yok sayılır.</param>
    /// <returns>İstemci kimliği.</returns>
    public static string Resolve(HttpContext? context, int trustedProxyHopCount)
    {
        if (context is null)
        {
            return Unknown;
        }

        if (trustedProxyHopCount > 0)
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                var entries = forwarded.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var index = entries.Length - trustedProxyHopCount;
                if (index >= 0 && index < entries.Length && entries[index].Length > 0)
                {
                    return entries[index];
                }
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? Unknown;
    }
}
