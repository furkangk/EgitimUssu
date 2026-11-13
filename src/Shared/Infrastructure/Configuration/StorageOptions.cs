namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// K-03: Dosya depolama yapılandırması. S3-uyumlu tek arayüz (AWS S3 / Cloudflare R2 / MinIO).
/// Sırlar repoda tutulmaz; <c>Storage__AccessKeyId</c> / <c>Storage__SecretAccessKey</c> ortam
/// değişkenleriyle verilir.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Geliştirmede diske yazan sağlayıcı.</summary>
    public const string LocalProvider = "Local";

    /// <summary>Üretimde S3-uyumlu nesne depolama.</summary>
    public const string S3Provider = "S3";

    /// <summary>"Local" (dev/test) veya "S3".</summary>
    public string Provider { get; set; } = LocalProvider;

    /// <summary>Yerel sağlayıcının kök dizini; anahtarlar bu dizinin dışına çıkamaz.</summary>
    public string LocalRootPath { get; set; } = "storage";

    public string BucketName { get; set; } = string.Empty;

    /// <summary>R2/MinIO gibi AWS-dışı uç noktalar için; doluysa path-style adresleme kullanılır.</summary>
    public string ServiceUrl { get; set; } = string.Empty;

    public string Region { get; set; } = "auto";

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;
}
