using EgitimUssu.Shared.Infrastructure.Configuration;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// A-01: Redis yapılandırması startup'ta doğrulanır.
/// <para>
/// Gerekçe diğer guard'lardan farklıdır: eksik Redis bir <i>hata</i> üretmez. Hız sınırı, token
/// kara listesi, giriş kilidi ve idempotency fail-open çalışır — hepsi sessizce kapanır, istekler
/// başarılı görünür, sağlık uçları yeşil kalır. Tek savunma yapılandırmanın startup'ta zorlanmasıdır.
/// </para>
/// </summary>
public sealed class RedisOptionsGuardTests
{
    [Fact]
    public void EnsureValid_Should_Throw_When_Configuration_Missing_In_Production()
    {
        var options = new RedisOptions { Configuration = string.Empty };

        Assert.Throws<InvalidOperationException>(() => RedisOptionsGuard.EnsureValid(options, isDevelopment: false));
    }

    /// <summary>
    /// Asıl yakalanmak istenen hata bu: <c>Redis__Configuration</c> ortam değişkeni verilmediğinde
    /// eskiden koddaki <c>"localhost:6379"</c> yedeği devreye giriyordu. Boş olmadığı için hiçbir
    /// kontrol şikâyet etmiyor, uygulama açılıyor ve dört koruma birden kapalı çalışıyordu.
    /// </summary>
    [Theory]
    [InlineData("localhost:6379")]
    [InlineData("127.0.0.1:6379")]
    [InlineData("LOCALHOST:6379")]
    [InlineData("[::1]:6379")]
    [InlineData("redis://localhost:6379")]
    [InlineData("redis://user:pass@localhost:6379")]
    public void EnsureValid_Should_Reject_Loopback_In_Production(string configuration)
    {
        var options = new RedisOptions { Configuration = configuration };

        Assert.Throws<InvalidOperationException>(() => RedisOptionsGuard.EnsureValid(options, isDevelopment: false));
    }

    [Theory]
    [InlineData("redis-prod.internal:6379")]
    [InlineData("rediss://default:secret@eu2-x.upstash.io:6379")]
    [InlineData("10.1.2.3:6379,abortConnect=false")]
    public void Validate_Should_Accept_Real_Endpoints(string configuration)
        => Assert.Null(RedisOptionsGuard.Validate(new RedisOptions { Configuration = configuration }, isDevelopment: false));

    /// <summary>
    /// Geliştirmede Redis'in ayakta olmaması normaldir; fail-open zaten istenen deneyimdir.
    /// Guard'ın burada fırlatması geliştiriciyi engellemekten başka işe yaramaz.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("localhost:6379")]
    public void Validate_Should_Pass_In_Development(string configuration)
        => Assert.Null(RedisOptionsGuard.Validate(new RedisOptions { Configuration = configuration }, isDevelopment: true));

    /// <summary>Varsayılan artık boştur — koddaki loopback yedeği kaldırıldı.</summary>
    [Fact]
    public void Default_Configuration_Should_Be_Empty()
        => Assert.Equal(string.Empty, new RedisOptions().Configuration);
}
