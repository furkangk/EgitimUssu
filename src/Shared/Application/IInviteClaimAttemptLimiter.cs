namespace EgitimUssu.Shared.Application;

/// <summary>
/// P15 / N-20: Davet kodu kullanma (claim) denemelerini <b>kullanıcı başına</b> sınırlar.
/// <para>
/// IP başına genel hız sınırı, kod tahminine karşı yetersizdi: saldırgan IP değiştirerek ya da
/// sınırın altında kalarak denemeye devam edebiliyordu. Sayaç kimliği doğrulanmış kullanıcıya
/// bağlıdır; eşik aşılınca pencere boyunca <b>doğru kod da</b> reddedilir — aksi hâlde kilit,
/// isabet anına kadar deneme sayısını sınırlamaktan başka bir şey yapmazdı.
/// </para>
/// </summary>
public interface IInviteClaimAttemptLimiter
{
    /// <summary>Kullanıcı şu an kilitli mi?</summary>
    /// <param name="userId">Kodu kullanmaya çalışan kullanıcı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Kilitliyse <c>true</c>.</returns>
    Task<bool> IsBlockedAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Başarısız bir denemeyi (bulunamayan ya da süresi dolmuş kod) sayar.</summary>
    /// <param name="userId">Kullanıcı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>İşlem görevi.</returns>
    Task RecordFailureAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Başarılı kullanımda sayacı sıfırlar.</summary>
    /// <param name="userId">Kullanıcı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>İşlem görevi.</returns>
    Task ResetAsync(Guid userId, CancellationToken cancellationToken);
}
