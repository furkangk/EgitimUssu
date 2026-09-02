using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Kernel.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Storage;

/// <summary>
/// K-03: S3-uyumlu nesne depolama (AWS S3 / Cloudflare R2 / MinIO). <see cref="StorageOptions.ServiceUrl"/>
/// doluysa path-style adresleme açılır — R2/MinIO bunu gerektirir.
/// Bucket **public değildir**; indirme uygulama üzerinden yetkiyle akıtılır.
/// </summary>
public sealed class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _client;
    private readonly StorageOptions _options;
    private readonly ILogger<S3FileStorage> _logger;

    public S3FileStorage(IOptions<StorageOptions> options, ILogger<S3FileStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger;
        _client = CreateClient(_options);
    }

    /// <summary>Testler için hazır bir istemci enjekte etmeye izin verir.</summary>
    internal S3FileStorage(IAmazonS3 client, IOptions<StorageOptions> options, ILogger<S3FileStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<StoredFile> SaveAsync(
        string keyPrefix,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var key = $"{keyPrefix.Trim('/')}/{Guid.NewGuid():N}{Path.GetExtension(fileName)}";

        // Uzunluğu önden bilmek gerekiyor (StoredFile.SizeBytes) ve akış ileri-sarılamayabilir.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        await _client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key,
                InputStream = buffer,
                ContentType = contentType,
                DisablePayloadSigning = !string.IsNullOrWhiteSpace(_options.ServiceUrl)
            },
            cancellationToken);

        return new StoredFile(key, fileName, contentType, buffer.Length);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        try
        {
            var response = await _client.GetObjectAsync(_options.BucketName, key, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Bilinmeyen anahtar hata değil: çağıran 404 üretir (sözleşme: null döner).
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        try
        {
            await _client.DeleteObjectAsync(_options.BucketName, key, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Silme idempotenttir.
            _logger.LogDebug("S3 silme: anahtar zaten yok ({Key}).", key);
        }
    }

    private static IAmazonS3 CreateClient(StorageOptions options)
    {
        var credentials = new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey);
        var config = new AmazonS3Config { ServiceURL = null };

        if (string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
        }
        else
        {
            // R2/MinIO: bölge yerine uç nokta + path-style adresleme.
            config.ServiceURL = options.ServiceUrl;
            config.ForcePathStyle = true;
            config.AuthenticationRegion = options.Region;
        }

        return new AmazonS3Client(credentials, config);
    }
}
