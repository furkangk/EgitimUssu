namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// Redis bağlantı ayarları. A-01: Varsayılan bilinçli olarak <b>boştur</b> — eskiden
/// <c>"localhost:6379"</c> idi ve üretimde <c>Redis__Configuration</c> verilmediğinde uygulama
/// sessizce erişilemeyen bir adrese bağlanmayı deneyip tüm Redis korumalarını fail-open bırakıyordu.
/// Değer <see cref="RedisOptionsGuard"/> ile startup'ta doğrulanır; geliştirme adresi
/// <c>appsettings.Development.json</c>'dadır.
/// </summary>
public sealed class RedisOptions
{
    /// <summary>Yapılandırma bölümü adı.</summary>
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis bağlantı dizesi.</summary>
    public string Configuration { get; set; } = string.Empty;
}
