namespace EgitimUssu.Shared.Kernel.Storage;

/// <summary>
/// K-03: Nesne depolama soyutlaması. Modüller sağlayıcıyı bilmez, yalnız **anahtar** (key) ile çalışır.
/// Dosyalar hiçbir zaman public URL ile sunulmaz; indirme daima uygulama üzerinden yetki kontrolüyle yapılır.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// İçeriği <c>{keyPrefix}/{yeniGuid}{uzantı}</c> anahtarıyla saklar; aynı adlı iki dosya çakışmaz.
    /// </summary>
    Task<StoredFile> SaveAsync(
        string keyPrefix,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Anahtar bilinmiyorsa veya geçersizse <c>null</c> döner (istisna fırlatmaz).</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Idempotenttir: olmayan anahtarı silmek hata değildir.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
