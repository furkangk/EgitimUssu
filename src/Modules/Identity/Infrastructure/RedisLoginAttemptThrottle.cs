using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EgitimUssu.Modules.Identity.Infrastructure;

/// <summary>
/// Y4 / A-16: Redis tabanlı giriş denemesi kilidi — <b>iki boyutlu</b>: (hesap + kaynak IP) ve (kaynak IP).
/// <para>
/// Eskiden sayaç yalnız e-postaya bağlıydı ve bu iki yönden de yanlıştı:
/// </para>
/// <list type="bullet">
/// <item><b>Hesap kilitleme DoS'u:</b> Bir e-posta adresini bilen herkes 5 hatalı denemeyle o
/// kullanıcıyı istediği an 15 dakika dışarıda bırakabiliyordu.</item>
/// <item><b>Password spraying:</b> Her hesaba tek deneme yapan bir saldırı sayaca hiç takılmıyordu;
/// onu yalnız IP hız sınırı durduruyordu, o da <c>X-Forwarded-For</c> ile atlanabiliyordu (A-02).</item>
/// </list>
/// <para>
/// Yeni davranış: bir hesabı hedefleyen kaba kuvvet (hesap + IP) çiftinde durur, aynı IP'den çok
/// sayıda hesaba yayılan deneme IP sayacında durur. Küresel hesap kilidi <b>bilinçli olarak
/// kaldırıldı</b> — hangi eşiğe konursa konsun DoS'a açıktır (OWASP da sert hesap kilidini
/// önermez). Dağıtık ve yavaş bir kaba kuvvete karşı savunma parola politikası + PBKDF2 + genel
/// hız sınırıdır.
/// </para>
/// <para>Redis erişilemezse <b>fail-open</b> (kilitlemez) — ADR-0004; düşüş A-01 sayacıyla ölçülür.</para>
/// </summary>
internal sealed class RedisLoginAttemptThrottle(
    ResilientRedisExecutor redis,
    IHttpContextAccessor httpContextAccessor,
    IOptions<RateLimitOptions> rateLimitOptions) : ILoginAttemptThrottle
{
    /// <summary>A-01: Redis düştüğünde hangi korumanın fail-open geçtiğini ölçen etiket.</summary>
    private const string Protection = "login-throttle";

    /// <summary>Tek bir hesaba, tek bir kaynaktan izin verilen ardışık başarısız deneme.</summary>
    private const int MaxAccountAttemptsPerSource = 5;

    /// <summary>
    /// Tek bir kaynaktan, hesap ayrımı gözetmeksizin izin verilen başarısız deneme. Meşru bir
    /// kullanıcının (hatta paylaşılan bir NAT arkasındaki birkaç kullanıcının) ulaşmayacağı,
    /// spraying'in ise ilk onlarca denemede çarpacağı bir eşik.
    /// </summary>
    private const int MaxSourceAttempts = 20;

    private const string AccountKeyPrefix = "login-fail:account:";
    private const string SourceKeyPrefix = "login-fail:source:";
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public async Task<bool> IsLockedAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var source = ResolveSource();

        var (ok, counts) = await redis.TryExecuteAsync(
            Protection,
            async database =>
            {
                var account = database.StringGetAsync(AccountKey(normalizedEmail, source));
                var sourceTotal = database.StringGetAsync(SourceKey(source));
                await Task.WhenAll(account, sourceTotal);
                return (Account: await account, Source: await sourceTotal);
            },
            cancellationToken);

        if (!ok)
        {
            return false; // fail-open (Redis erişilemez)
        }

        return Exceeds(counts.Account, MaxAccountAttemptsPerSource)
            || Exceeds(counts.Source, MaxSourceAttempts);
    }

    /// <inheritdoc />
    public Task RecordFailureAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var source = ResolveSource();

        return redis.TryExecuteAsync(
            Protection,
            async database =>
            {
                await IncrementAsync(database, AccountKey(normalizedEmail, source));
                await IncrementAsync(database, SourceKey(source));
                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task ResetAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var source = ResolveSource();

        // Yalnız hesap sayacı sıfırlanır. Kaynak sayacı BİLEREK durur: başarılı bir girişin
        // aynı IP'den süren spraying'i temizlemesi, saldırgana kendi sayacını sıfırlama yolu verirdi
        // (kendi hesabına giriş yapıp devam etmek yeterdi).
        return redis.TryExecuteAsync(
            Protection,
            database => database.KeyDeleteAsync(AccountKey(normalizedEmail, source)),
            cancellationToken);
    }

    private static bool Exceeds(RedisValue value, int threshold)
        => !value.IsNullOrEmpty && value.TryParse(out long count) && count >= threshold;

    private static async Task IncrementAsync(IDatabase database, RedisKey key)
    {
        var count = await database.StringIncrementAsync(key);
        if (count == 1)
        {
            await database.KeyExpireAsync(key, Window);
        }
    }

    private string ResolveSource() => ClientIpResolver.Resolve(
        httpContextAccessor.HttpContext,
        rateLimitOptions.Value.TrustedProxyHopCount);

    private static RedisKey AccountKey(string normalizedEmail, string source)
        => new($"{AccountKeyPrefix}{normalizedEmail}:{source}");

    private static RedisKey SourceKey(string source) => new($"{SourceKeyPrefix}{source}");
}
