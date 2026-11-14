namespace EgitimUssu.Shared.Infrastructure.Authentication;

/// <summary>
/// P15 T7 / N-31: Erişim token'ı doğrulanırken hesabın hâlâ kullanılabilir olup olmadığı.
/// <para>
/// İmza geçerli bir token hesap askıya alındıktan ya da kapandıktan sonra da ömrünü (60 dk)
/// tamamlayana kadar geçiyordu. <c>OnTokenValidated</c> her istekte bu soruyu sorar; uygulama
/// (Identity) cevabı kısa süre önbellekler — her istekte veritabanına gidilmez.
/// </para>
/// <para>
/// Port host katmanında, uygulaması Identity modülündedir: host'un Identity'nin DbContext'ini
/// bilmesine gerek kalmaz.
/// </para>
/// </summary>
public interface IAccountStatusReader
{
    /// <summary>Hesap oturum açmaya uygun mu (etkin ya da etkinleştirme bekliyor)?</summary>
    /// <param name="userId">Token'ın sahibi.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Hesap yoksa ya da askıda/kapalıysa <c>false</c>.</returns>
    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken);
}
