using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Kernel.Storage;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Storage;

/// <summary>
/// Diske yazan geliştirme sağlayıcısı (<c>Storage:Provider = "Local"</c>).
/// Render gibi ortamlarda disk **kalıcı değildir**; üretimde <see cref="S3FileStorage"/> kullanılır.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _rootPath = Path.GetFullPath(options.Value.LocalRootPath);
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
        var path = ResolvePath(key)
            ?? throw new InvalidOperationException($"Gecersiz depolama anahtari: {key}");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var target = File.Create(path);
        await content.CopyToAsync(target, cancellationToken);

        return new StoredFile(key, fileName, contentType, target.Length);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (path is null || !File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(File.OpenRead(path));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Anahtarı kök dizin altındaki bir yola çevirir. Kökün **dışına** çıkan her anahtar
    /// (<c>../</c>, mutlak yol) <c>null</c> döner — path traversal engeli.
    /// </summary>
    private string? ResolvePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Path.IsPathRooted(key))
        {
            return null;
        }

        var candidate = Path.GetFullPath(Path.Combine(_rootPath, key));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        return candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal) ? candidate : null;
    }
}
