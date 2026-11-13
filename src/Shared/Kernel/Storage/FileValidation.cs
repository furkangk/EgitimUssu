namespace EgitimUssu.Shared.Kernel.Storage;

/// <summary>
/// P04: Yükleme sınırı ve tür beyaz listesi. **Sunucu tarafı** kuraldır — istemci doğrulamasına güvenilmez.
/// </summary>
public static class FileValidation
{
    /// <summary>Yükleme başına üst sınır: 10 MB.</summary>
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".doc", ".docx",
        ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".zip"
    };

    private static readonly Error TooLarge = new("storage.file_too_large", "Dosya 10 MB sinirini asiyor.");
    private static readonly Error Unsupported = new("storage.unsupported_type", "Bu dosya turu desteklenmiyor.");
    private static readonly Error Empty = new("storage.empty_file", "Bos dosya yuklenemez.");

    /// <summary>İzinli uzantılar (küçük harfli, nokta dahil).</summary>
    public static IReadOnlyCollection<string> SupportedExtensions => AllowedExtensions;

    public static Result Validate(string fileName, long sizeBytes)
    {
        if (sizeBytes <= 0)
        {
            return Result.Failure(Empty);
        }

        if (sizeBytes > MaxSizeBytes)
        {
            return Result.Failure(TooLarge);
        }

        var extension = Path.GetExtension(fileName);
        return AllowedExtensions.Contains(extension) ? Result.Success() : Result.Failure(Unsupported);
    }
}
