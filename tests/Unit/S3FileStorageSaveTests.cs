using Amazon.S3;
using Amazon.S3.Model;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P04 Task 5 regresyonu: AWS SDK <c>PutObjectAsync</c> sonrası <c>InputStream</c>'i **kapatır**.
/// Uzunluk çağrıdan sonra okunursa <c>ObjectDisposedException</c> alınır — bu hata gerçek
/// MinIO'ya karşı yapılan doğrulamada yakalandı; test onu kalıcı olarak kilitler.
/// </summary>
public sealed class S3FileStorageSaveTests
{
    [Fact]
    public async Task SaveAsync_Should_Report_Size_Even_When_Sdk_Closes_Stream()
    {
        var client = new StreamClosingS3Client();
        var storage = new S3FileStorage(
            client,
            Options.Create(new StorageOptions
            {
                Provider = StorageOptions.S3Provider,
                BucketName = "egitimussu",
                ServiceUrl = "http://127.0.0.1:9010",
                AccessKeyId = "key",
                SecretAccessKey = "secret"
            }),
            NullLogger<S3FileStorage>.Instance);

        using var content = new MemoryStream(new byte[42]);

        var stored = await storage.SaveAsync("teachers/abc", "foto.png", "image/png", content);

        Assert.Equal(42, stored.SizeBytes);
        Assert.StartsWith("teachers/abc/", stored.Key, StringComparison.Ordinal);
        Assert.EndsWith(".png", stored.Key, StringComparison.Ordinal);
        Assert.Equal("egitimussu", client.LastBucket);
    }

    /// <summary>Gerçek SDK davranışını taklit eder: gönderim sonrası akışı kapatır.</summary>
    private sealed class StreamClosingS3Client : AmazonS3Client
    {
        public StreamClosingS3Client()
            : base("key", "secret", new AmazonS3Config { ServiceURL = "http://127.0.0.1:9010", ForcePathStyle = true })
        {
        }

        public string? LastBucket { get; private set; }

        public override Task<PutObjectResponse> PutObjectAsync(
            PutObjectRequest request,
            CancellationToken cancellationToken = default)
        {
            LastBucket = request.BucketName;
            request.InputStream.Dispose();
            return Task.FromResult(new PutObjectResponse());
        }
    }
}
