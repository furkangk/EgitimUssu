namespace EgitimUssu.Shared.Kernel;

/// <summary>
/// P15 / K-24: İnsanın elle gireceği tahmin edilemez kodlar (davet, sınıfa katılım).
/// Kriptografik rastgelelik kullanır; <c>Random.Shared</c> gibi tahmin edilebilir kaynaklar yasaktır.
/// </summary>
public interface ISecureCodeGenerator
{
    /// <summary>Verilen uzunlukta, <see cref="SecureCode.Alphabet"/>'ten yeni bir kod üretir.</summary>
    /// <param name="length">Kod uzunluğu; en az <see cref="SecureCode.MinLength"/>.</param>
    /// <returns>Büyük harfli kod.</returns>
    string NewCode(int length = SecureCode.DefaultLength);
}

/// <summary>Güvenli kodların biçim kuralları — üretici ve karşılaştıran taraf aynı kuralı kullanır.</summary>
public static class SecureCode
{
    /// <summary>
    /// Karışmayan 31 karakter: <c>0/O</c>, <c>1/I/L</c> yok. 8 karakterde 31⁸ ≈ 8,5×10¹¹ olasılık
    /// (eski 6 haneli sayısal kod: 10⁶).
    /// </summary>
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    /// <summary>Varsayılan kod uzunluğu.</summary>
    public const int DefaultLength = 8;

    /// <summary>Kabul edilen en kısa uzunluk.</summary>
    public const int MinLength = 6;

    /// <summary>Kullanıcının girdiği kodu karşılaştırma biçimine getirir: kırpar ve büyük harfe çevirir.</summary>
    /// <param name="code">Girilen kod.</param>
    /// <returns>Normalize edilmiş kod; <c>null</c> ise boş dize.</returns>
    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();
}
