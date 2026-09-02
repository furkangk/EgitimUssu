using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Storage;
using EgitimUssu.Shared.Kernel.Storage;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P04/K-03: Geliştirme depolaması. Kritik güvenlik davranışı: anahtar kök dizin dışına çıkamaz
/// (path traversal). Bilinmeyen anahtar istisna değil <c>null</c> döner (çağıran 404 üretir).
/// </summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"eu-storage-{Guid.NewGuid():N}");

    [Fact]
    public async Task Save_Then_OpenRead_Should_Return_Same_Content()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream("merhaba"u8.ToArray());

        var stored = await storage.SaveAsync("assignments/abc", "odev.txt", "text/plain", content);

        Assert.Equal("odev.txt", stored.FileName);
        Assert.Equal("text/plain", stored.ContentType);
        Assert.Equal(7, stored.SizeBytes);
        Assert.StartsWith("assignments/abc/", stored.Key, StringComparison.Ordinal);
        Assert.EndsWith(".txt", stored.Key, StringComparison.Ordinal);

        await using var read = await storage.OpenReadAsync(stored.Key);
        Assert.NotNull(read);
        using var reader = new StreamReader(read!);
        Assert.Equal("merhaba", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Delete_Should_Remove_File()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream("merhaba"u8.ToArray());
        var stored = await storage.SaveAsync("assignments/abc", "odev.txt", "text/plain", content);

        await storage.DeleteAsync(stored.Key);

        Assert.Null(await storage.OpenReadAsync(stored.Key));
    }

    [Fact]
    public async Task Delete_Unknown_Key_Should_Be_Idempotent()
    {
        var storage = CreateStorage();

        await storage.DeleteAsync("assignments/abc/yok.txt");
    }

    [Fact]
    public async Task Unknown_Key_Should_Return_Null()
        => Assert.Null(await CreateStorage().OpenReadAsync("assignments/abc/yok.txt"));

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("assignments/../../../etc/passwd")]
    [InlineData("/etc/passwd")]
    public async Task Key_Should_Not_Allow_Path_Traversal(string key)
        => Assert.Null(await CreateStorage().OpenReadAsync(key));

    [Fact]
    public async Task Two_Saves_Of_Same_Name_Should_Not_Collide()
    {
        var storage = CreateStorage();
        using var first = new MemoryStream("bir"u8.ToArray());
        using var second = new MemoryStream("iki"u8.ToArray());

        var a = await storage.SaveAsync("assignments/abc", "odev.txt", "text/plain", first);
        var b = await storage.SaveAsync("assignments/abc", "odev.txt", "text/plain", second);

        Assert.NotEqual(a.Key, b.Key);
    }

    private LocalFileStorage CreateStorage()
        => new(Options.Create(new StorageOptions { LocalRootPath = _root }));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
