namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// K-02: Push bildirim yapılandırması. Tek kanal FCM (Android + iOS/APNs proxy).
/// Service-account sırrı repoda tutulmaz; yalnız <c>Push__ServiceAccountJson</c> ortam değişkeni
/// ya da dosya yolu (<c>Push__ServiceAccountJsonPath</c>) ile verilir.
/// </summary>
public sealed class PushOptions
{
    public const string SectionName = "Push";

    /// <summary>Geliştirme/testte log'a basan sağlayıcı.</summary>
    public const string LoggingProvider = "Logging";

    /// <summary>Üretimde Firebase Cloud Messaging HTTP v1.</summary>
    public const string FcmProvider = "Fcm";

    /// <summary>"Logging" (dev/test) veya "Fcm".</summary>
    public string Provider { get; set; } = LoggingProvider;

    /// <summary>Firebase proje kimliği; FCM uç noktasının yolunda kullanılır.</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Service-account JSON dosyasının yolu (dosya olarak mount edilen ortamlar için).</summary>
    public string ServiceAccountJsonPath { get; set; } = string.Empty;

    /// <summary>Service-account JSON içeriği (Render gibi yalnız ortam değişkeni veren ortamlar için).</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;
}
