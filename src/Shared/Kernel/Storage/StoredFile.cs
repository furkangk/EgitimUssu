namespace EgitimUssu.Shared.Kernel.Storage;

/// <summary>
/// Depoda saklanan bir dosyanın kimliği ve meta verisi.
/// <paramref name="Key"/> depolama anahtarıdır (public URL değildir), <paramref name="FileName"/> ise
/// kullanıcıya gösterilecek özgün ad.
/// </summary>
public sealed record StoredFile(string Key, string FileName, string ContentType, long SizeBytes);
