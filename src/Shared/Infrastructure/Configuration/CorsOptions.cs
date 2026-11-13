namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// A-17: Tarayıcı istemcileri için CORS ayarları.
/// <para>
/// Varsayılan <b>boş</b>: mobil istemci CORS'a tabi değildir, dolayısıyla hiçbir köken açılmaz.
/// Planlanan Angular istemci geldiğinde köken listesi <c>Cors__AllowedOrigins__0</c> gibi ortam
/// değişkenleriyle verilir. Joker (<c>*</c>) bilinçli olarak <b>desteklenmez</b>: kimlik bilgisi
/// taşıyan isteklerde zaten geçersizdir ve "geçici olarak açalım" kararı kalıcılaşır.
/// </para>
/// </summary>
public sealed class CorsOptions
{
    /// <summary>Yapılandırma bölümü adı.</summary>
    public const string SectionName = "Cors";

    /// <summary>Adlandırılmış politikanın adı.</summary>
    public const string PolicyName = "EgitimUssuClients";

    /// <summary>İzin verilen kökenler (şema + host + port). Boşsa CORS hiç uygulanmaz.</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>En az bir köken tanımlı mı.</summary>
    public bool HasOrigins => AllowedOrigins.Length > 0;
}
