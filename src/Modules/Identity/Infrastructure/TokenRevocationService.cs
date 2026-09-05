using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Modules.Identity.Infrastructure;

/// <summary>
/// A-06: <see cref="ITokenRevocationService"/>'in Redis destekli uygulaması.
/// <para>
/// Kullanıcı başına bir "şu andan önce verilenler geçersiz" damgası yazar; damga
/// <c>OnTokenValidated</c>'da token'ın <c>iat</c> claim'iyle karşılaştırılır. Tek tek <c>jti</c>
/// kara listelemek bu iş için yetmez — parola sıfırlanırken saldırganın elindeki token'ın kimliği
/// bilinmez.
/// </para>
/// <para>
/// Damganın ömrü erişim token'ı ömrü kadardır (küçük bir emniyet payıyla): o süre geçtikten sonra
/// eski token'lar zaten <c>exp</c> ile reddedilir, kaydı tutmanın anlamı kalmaz.
/// </para>
/// </summary>
internal sealed class TokenRevocationService(
    ITokenBlacklist tokenBlacklist,
    IOptions<JwtOptions> jwtOptions,
    IClock clock) : ITokenRevocationService
{
    /// <summary>Saat kayması ve yazma gecikmesi için damgaya eklenen pay.</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public Task RevokeAllAccessTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var timeToLive = TimeSpan.FromMinutes(jwtOptions.Value.ExpiryMinutes) + Grace;

        return tokenBlacklist.RevokeUserTokensIssuedBeforeAsync(
            userId.ToString(),
            now,
            timeToLive,
            cancellationToken);
    }
}
