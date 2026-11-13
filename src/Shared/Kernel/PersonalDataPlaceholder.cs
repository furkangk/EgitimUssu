namespace EgitimUssu.Shared.Kernel;

/// <summary>
/// E-07: Anonimleştirmede kullanılan yer tutucular.
/// <para>
/// 🔴 <b>Vekil e-posta hash ile üretilmez.</b> "E-postayı hash'leyip <c>deleted-{hash}@…</c> yazmak"
/// yeterli görünür ama değildir: e-posta adresleri sınırlı ve tahmin edilebilir bir kümedir, aynı
/// hash bilinen bir adres listesine uygulanarak <b>eşleştirilebilir</b>. Bu anonimleştirme değil
/// takma adlaştırmadır ve takma adlı veri hâlâ kişisel veridir — yani silme talebi gerçekte yerine
/// getirilmemiş olur. Bu yüzden vekil değer, silinen hesapla <b>hiçbir</b> matematiksel ilişkisi
/// olmayan rastgele bir kimlikten üretilir.
/// (Ayrıntı: <c>doc/legal/kvkk_saklama_imha_politikasi.md</c> §4.)
/// </para>
/// </summary>
public static class PersonalDataPlaceholder
{
    /// <summary>Silinen kullanıcının görünen adı.</summary>
    public const string FullName = "Silinmiş Kullanıcı";

    /// <summary>Silinen içeriğin yerine yazılan metin.</summary>
    public const string RemovedContent = "[silindi]";

    /// <summary>
    /// Silinen hesap için, kaynağıyla ilişkisiz bir vekil e-posta üretir.
    /// </summary>
    /// <param name="surrogateId">
    /// <c>IIdGenerator</c>'dan alınan rastgele kimlik. Silinen e-postadan <b>türetilmemelidir</b>.
    /// </param>
    public static string DeletedEmail(Guid surrogateId) => $"deleted-{surrogateId:N}@egitimussu.local";
}
