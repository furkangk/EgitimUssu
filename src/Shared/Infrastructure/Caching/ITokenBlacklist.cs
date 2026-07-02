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
}
