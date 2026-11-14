using EgitimUssu.Shared.Infrastructure.Configuration;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P04/K-03: S3 seçiliyken eksik ayarla uygulama açılmaz (fail-fast, `EmailOptionsGuard` deseni).
/// </summary>
public sealed class StorageOptionsGuardTests
{
    [Fact]
    public void EnsureValid_Should_Throw_When_Bucket_Missing()
    {
        var options = new StorageOptions { Provider = StorageOptions.S3Provider };

        Assert.Throws<InvalidOperationException>(() => StorageOptionsGuard.EnsureValid(options, "Production"));
    }

    [Fact]
    public void EnsureValid_Should_Throw_When_Credentials_Missing()
    {
        var options = new StorageOptions { Provider = StorageOptions.S3Provider, BucketName = "egitimussu" };

        Assert.Throws<InvalidOperationException>(() => StorageOptionsGuard.EnsureValid(options, "Production"));
    }

    [Fact]
    public void EnsureValid_Should_Reject_Unknown_Provider()
    {
        var options = new StorageOptions { Provider = "AzureBlob" };

        Assert.Throws<InvalidOperationException>(() => StorageOptionsGuard.EnsureValid(options, "Production"));
    }

    [Fact]
    public void EnsureValid_Should_Allow_Complete_S3_Configuration()
    {
        var options = new StorageOptions
        {
            Provider = StorageOptions.S3Provider,
            BucketName = "egitimussu",
            AccessKeyId = "key",
            SecretAccessKey = "secret"
        };

        StorageOptionsGuard.EnsureValid(options, "Production");
    }

    /// <summary>
    /// P15 T6 / P-01: Local yalnız Development/Staging'de (eskiden üretimde de geçiyordu — Render'ın
    /// geçici diskinde yüklenen dosyalar her deploy'da kaybolurdu). Ortam kuralının tamamı
    /// <see cref="ProductionProviderGuardTests"/>'te.
    /// </summary>
    [Fact]
    public void Validate_Should_Pass_For_Local_Provider_Only_In_Sandbox_Environments()
    {
        Assert.Null(StorageOptionsGuard.Validate(new StorageOptions(), "Development"));
        Assert.Null(StorageOptionsGuard.Validate(new StorageOptions(), "Staging"));
        Assert.NotNull(StorageOptionsGuard.Validate(new StorageOptions(), "Production"));
    }

    [Fact]
    public void Validate_Should_Reject_Empty_Local_Root_Path()
    {
        var options = new StorageOptions { LocalRootPath = "  " };

        var error = StorageOptionsGuard.Validate(options, "Development");

        Assert.NotNull(error);
        Assert.Contains("Storage:LocalRootPath", error);
    }
}
