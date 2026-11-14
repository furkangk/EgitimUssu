namespace EgitimUssu.Shared.Infrastructure.Caching;

/// <summary>
/// Y4: Erişim token'larının (jti) anlık iptali için blacklist. Logout / yetki-düşüşünde token,
/// kalan ömrü boyunca kara listeye alınır; JWT doğrulamasında kontrol edilir. Redis erişilemezse
/// <b>fail-open</b> (token geçerli sayılır) — ADR-0004 kararı.
/// </summary>
public interface ITokenBlacklist
{
    Task BlacklistAsync(string tokenId, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    Task<bool> IsBlacklistedAsync(string tokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A-06: Bir kullanıcının <paramref name="cutoffUtc"/> anından <b>önce</b> verilmiş tüm erişim
    /// token'larını geçersiz kılar.
    /// <para>
    /// Tek tek <c>jti</c> kara listelemek yetmez: parola sıfırlandığında ya da rol alındığında
    /// saldırganın (ya da eski yetkinin) elindeki token'ın kimliği bilinmez. Refresh oturumlarını
    /// iptal etmek de yetmiyordu — erişim token'ı ömrünü (60 dk) tamamlayana kadar geçerli kalıyordu.
    /// </para>
    /// </summary>
    /// <param name="userId">Kullanıcı kimliği.</param>
    /// <param name="cutoffUtc">Bu andan önce verilmiş token'lar reddedilir.</param>
    /// <param name="timeToLive">Kaydın saklama süresi; erişim token'ı ömründen kısa olmamalı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>İşlem görevi.</returns>
    Task RevokeUserTokensIssuedBeforeAsync(
        string userId,
        DateTime cutoffUtc,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// P15 T7 / N-31: Verilen oturumlardan (<c>sid</c>) verilmiş erişim token'larını reddedilir kılar.
    /// <para>
    /// Cihaz iptali ve "diğerlerini kapat" kullanıcı damgasını kullanamaz: damga kullanıcının
    /// <b>şu anki</b> cihazını da atardı. Kayıt oturum başınadır ve <paramref name="timeToLive"/>
    /// sonra kendiliğinden düşer (o ana dek o oturumun token'ları zaten <c>exp</c> ile ölmüştür).
    /// </para>
    /// </summary>
    /// <param name="sessionIds">Kapatılan oturum kimlikleri.</param>
    /// <param name="timeToLive">Kaydın saklama süresi; erişim token'ı ömründen kısa olmamalı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>İşlem görevi.</returns>
    Task RevokeSessionsAsync(
        IReadOnlyCollection<string> sessionIds,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A-06 + P15 T7: Token <c>jti</c> kara listesine, kullanıcı bazlı iptal damgasına ve oturum
    /// (<c>sid</c>) iptal kümesine karşı denetlenir. Üç kontrol tek Redis turunda yapılır — istek
    /// başına ek gecikme getirmez.
    /// </summary>
    /// <param name="tokenId">Token'ın <c>jti</c> değeri.</param>
    /// <param name="userId">Token'ın sahibi; yoksa damga denetlenmez.</param>
    /// <param name="sessionId">Token'ın oturumu (<c>sid</c>); yoksa oturum kümesi denetlenmez.</param>
    /// <param name="issuedAtUtc">Token'ın veriliş anı (milisaniye hassasiyetinde); yoksa damga denetlenemez.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Token iptal edilmişse <c>true</c>.</returns>
    Task<bool> IsRevokedAsync(
        string tokenId,
        string? userId,
        string? sessionId,
        DateTime? issuedAtUtc,
        CancellationToken cancellationToken = default);
}
