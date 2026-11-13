namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// C-04 / K-10: Gözlemlenebilirlik ayarları. Varsayılan **kapalıdır**: testte ve CI'da hiçbir
/// exporter kurulmaz, maliyet sıfır kalır. Üretimde `Observability__Enabled=true` ile açılır.
/// </summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>Açıkken trace + metrik toplanır ve dışa aktarılır.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// OTLP toplayıcı adresi (ör. `http://localhost:4317`). **Boşsa konsol exporter** kullanılır —
    /// yerelde toplayıcı kurmadan da zincirin çalıştığı görülebilsin diye.
    /// </summary>
    public string OtlpEndpoint { get; set; } = string.Empty;

    /// <summary>Toplayıcıda görünecek servis adı.</summary>
    public string ServiceName { get; set; } = "egitimussu-api";

    /// <summary>Servis sürümü; boşsa çalışan derlemenin sürümü kullanılır.</summary>
    public string ServiceVersion { get; set; } = string.Empty;

    /// <summary>OTLP hedefi tanımlı mı (yoksa konsola yazılır).</summary>
    public bool HasOtlpEndpoint => !string.IsNullOrWhiteSpace(OtlpEndpoint);
}
