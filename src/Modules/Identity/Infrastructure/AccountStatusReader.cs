using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Infrastructure.Authentication;
using EgitimUssu.Shared.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace EgitimUssu.Modules.Identity.Infrastructure;

/// <summary>
/// P15 T7 / N-31: Hesap durumunu her istekte sorar, cevabı Redis'te kısa süre önbellekler.
/// <para>
/// <b>Önbellek süresi (<see cref="CacheDuration"/>) bayatlığın üst sınırıdır:</b> durum veritabanında
/// değiştikten sonra en çok bu kadar eski cevap verilebilir. Hesabı kapatan/askıya alan akış
/// bununla yetinmez, dolaşımdaki token'ları ayrıca kullanıcı damgasıyla anında iptal eder
/// (<c>ITokenRevocationService.RevokeAllAccessTokensAsync</c> — ör. KVKK silme işlemcisi). Bu kontrol
/// o adımı unutan ya da veritabanında elle yapılan değişikliklere karşı ikinci savunma hattıdır.
/// </para>
/// <para>
/// Redis erişilemezse veritabanına gidilir (fail-open değil — veritabanı zaten isteğin geri kalanı
/// için gerekli, cevabı da yetkilidir). Hesap yoksa <c>false</c>.
/// </para>
/// </summary>
internal sealed class AccountStatusReader(IdentityDbContext dbContext, ResilientRedisExecutor redis) : IAccountStatusReader
{
    /// <summary>A-01: Redis düştüğünde hangi korumanın veritabanına düştüğünü ölçen etiket.</summary>
    private const string Protection = "account-status";

    private const string KeyPrefix = "account-active:";

    /// <summary>Önbellekteki cevabın ömrü — durum değişikliğinin en geç yansıma süresi.</summary>
    internal static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var key = new RedisKey($"{KeyPrefix}{userId}");
        var (cached, value) = await redis.TryExecuteAsync(
            Protection, database => database.StringGetAsync(key), cancellationToken);
        if (cached && value.HasValue)
        {
            return value == "1";
        }

        var status = await dbContext.UserAccounts.AsNoTracking()
            .Where(account => account.Id == userId)
            .Select(account => (UserAccountStatus?)account.Status)
            .FirstOrDefaultAsync(cancellationToken);
        var active = status is UserAccountStatus.Active or UserAccountStatus.PendingActivation;

        await redis.TryExecuteAsync(
            Protection,
            database => database.StringSetAsync(key, active ? "1" : "0", CacheDuration),
            cancellationToken);
        return active;
    }
}
