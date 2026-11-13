using EgitimUssu.Shared.Infrastructure.Storage;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P04 Task 5: R2/MinIO uyum ayarları. Gerçek MinIO'ya karşı yapılan doğrulamada bulunan hata:
/// <c>DisablePayloadSigning = true</c> AWS SDK'da **yalnız HTTPS** ile geçerlidir; HTTP bir uç
/// noktada istek imzalanırken <c>AmazonClientException</c> fırlatılır. Bu yüzden bayrak
/// yalnızca HTTPS uç noktalarda açılır (R2 daima HTTPS'tir; yerel MinIO genelde HTTP).
/// </summary>
public sealed class S3FileStorageOptionsTests
{
    [Fact]
    public void Payload_Signing_Should_Stay_Enabled_For_Aws()
        => Assert.False(S3FileStorage.ShouldDisablePayloadSigning(string.Empty));

    [Fact]
    public void Payload_Signing_Should_Stay_Enabled_For_Http_Endpoint()
        => Assert.False(S3FileStorage.ShouldDisablePayloadSigning("http://127.0.0.1:9010"));

    [Fact]
    public void Payload_Signing_Should_Be_Disabled_For_Https_Endpoint()
        => Assert.True(S3FileStorage.ShouldDisablePayloadSigning("https://hesap.r2.cloudflarestorage.com"));

    [Fact]
    public void Path_Style_Should_Be_Used_Only_With_Custom_Endpoint()
    {
        Assert.False(S3FileStorage.ShouldUsePathStyle(string.Empty));
        Assert.True(S3FileStorage.ShouldUsePathStyle("http://127.0.0.1:9010"));
    }
}
