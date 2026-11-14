using System.Net;
using System.Security.Claims;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Tests.Unit;

public sealed class DistributedRateLimitMiddlewareTests
{
    [Fact]
    public async Task Denied_Auth_Request_Returns_429_And_Skips_Next()
    {
        var limiter = new FakeRateLimiter(allow: false);
        var nextCalled = false;
        var middleware = Build(limiter, _ => { nextCalled = true; return Task.CompletedTask; });
        var context = MakeContext("/api/identity/login");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.StartsWith("auth:", limiter.LastKey);
    }

    /// <summary>
    /// P15 T7 / N-32: Kimlik bilgisi alan uçlar (giriş, kayıt, şifre sıfırlama, e-posta doğrulama)
    /// sıkı IP kovasında kalır; hesap boyutu <c>ILoginAttemptThrottle</c>'dadır.
    /// </summary>
    [Theory]
    [InlineData("/api/identity/login")]
    [InlineData("/api/identity/register")]
    [InlineData("/api/identity/password-reset/request")]
    [InlineData("/api/identity/password-reset/confirm")]
    [InlineData("/api/identity/email-verification/request")]
    [InlineData("/api/identity/email-verification/confirm")]
    public async Task Credential_Endpoints_Stay_On_The_Strict_Ip_Policy(string path)
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);
        var context = MakeContext(path, method: "POST", userId: "u-1");

        await middleware.InvokeAsync(context);

        Assert.Equal("auth:1.2.3.4", limiter.LastKey);
        Assert.Equal(new RateLimitOptions().Auth.PermitLimit, limiter.LastPermitLimit);
    }

    /// <summary>
    /// N-32: Oturum açmış kullanıcının kimlik uçları (<c>/me</c>, <c>/sessions</c>, <c>/logout</c>…)
    /// <b>kullanıcı</b> başına sayılır. IP başına 10/dk iken okul NAT'ı arkasındaki bir sınıf
    /// birbirinin kotasını tüketiyordu.
    /// </summary>
    [Theory]
    [InlineData("/api/identity/me", "GET")]
    [InlineData("/api/identity/sessions", "GET")]
    [InlineData("/api/identity/sessions/revoke-all", "POST")]
    [InlineData("/api/identity/logout", "POST")]
    public async Task Authenticated_Identity_Endpoints_Are_Counted_Per_User(string path, string method)
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);
        var context = MakeContext(path, method: method, userId: "u-42");

        await middleware.InvokeAsync(context);

        Assert.Equal("session:u-42", limiter.LastKey);
        Assert.Equal(new RateLimitOptions().Session.PermitLimit, limiter.LastPermitLimit);
    }

    /// <summary>
    /// N-32: <c>/refresh</c> anonimdir (erişim token'ı süresi dolmuş, mobil bearer göndermez) —
    /// kullanıcı başına sayılamaz. Ayrı ve daha gevşek bir IP kovasıdır: 48 baytlık rastgele token
    /// kaba kuvvete açık değil, kova yalnız taşkına karşıdır.
    /// </summary>
    [Fact]
    public async Task Refresh_Uses_Its_Own_Looser_Ip_Policy()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);
        var context = MakeContext("/api/identity/refresh", method: "POST");

        await middleware.InvokeAsync(context);

        var options = new RateLimitOptions();
        Assert.Equal("refresh:1.2.3.4", limiter.LastKey);
        Assert.Equal(options.Refresh.PermitLimit, limiter.LastPermitLimit);
        Assert.True(options.Refresh.PermitLimit > options.Auth.PermitLimit);
    }

    [Fact]
    public async Task Allowed_Business_Request_Calls_Next_With_Default_Policy()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var nextCalled = false;
        var middleware = Build(limiter, _ => { nextCalled = true; return Task.CompletedTask; });
        var context = MakeContext("/api/payments/records");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.StartsWith("default:", limiter.LastKey);
    }

    [Fact]
    public async Task NonApi_Path_Bypasses_Limiter()
    {
        var limiter = new FakeRateLimiter(allow: false);
        var nextCalled = false;
        var middleware = Build(limiter, _ => { nextCalled = true; return Task.CompletedTask; });
        var context = MakeContext("/health/ready");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled); // fail-open by design: sağlık/altyapı uçları limitlenmez
        Assert.Equal(0, limiter.Calls);
    }

    /// <summary>
    /// A-02: İstemci IP'si <c>X-Forwarded-For</c>'un <b>sonundan</b> güvenilir proxy sayısı kadar
    /// geriden okunur — o girdiyi bizim önümüzdeki proxy yazmıştır. Tek proxy (Render) → son girdi.
    /// </summary>
    [Fact]
    public async Task Partition_Key_Uses_Ip_Appended_By_The_Trusted_Proxy()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);
        var context = MakeContext("/api/identity/login", forwardedFor: "9.9.9.9, 10.0.0.1");

        await middleware.InvokeAsync(context);

        Assert.Equal("auth:10.0.0.1", limiter.LastKey);
    }

    /// <summary>
    /// A-02 regresyonu: İstemcinin uydurduğu <c>X-Forwarded-For</c> partition'ı değiştirememeli.
    /// Eskiden listenin <b>ilk</b> girdisi alınıyordu; istemci her istekte farklı bir değer
    /// göndererek kendine taze bir kova açıyor ve kimlik uçlarındaki sınırı tümüyle atlıyordu.
    /// </summary>
    [Fact]
    public async Task Spoofed_Forwarded_For_Cannot_Change_The_Partition()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);

        var first = MakeContext(
            "/api/identity/login", forwardedFor: "1.1.1.1, 203.0.113.7", remoteIp: "10.0.0.1");
        await middleware.InvokeAsync(first);
        var firstKey = limiter.LastKey;

        var second = MakeContext(
            "/api/identity/login", forwardedFor: "2.2.2.2, 203.0.113.7", remoteIp: "10.0.0.1");
        await middleware.InvokeAsync(second);

        Assert.Equal(firstKey, limiter.LastKey);
        Assert.Equal("auth:203.0.113.7", limiter.LastKey);
    }

    /// <summary>
    /// İstemci başlık göndermediğinde proxy kendi gördüğü adresi tek girdi olarak ekler; bu girdi
    /// güvenilirdir ve kullanılır (tek proxy → son girdi zaten ilk girdidir).
    /// </summary>
    [Fact]
    public async Task Single_Entry_Chain_Is_The_Proxy_Append()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);
        var context = MakeContext("/api/identity/login", forwardedFor: "203.0.113.7", remoteIp: "10.0.0.1");

        await middleware.InvokeAsync(context);

        Assert.Equal("auth:203.0.113.7", limiter.LastKey);
    }

    /// <summary>Başlık hiç yoksa soket adresine düşülür.</summary>
    [Fact]
    public async Task Missing_Forwarded_Header_Falls_Back_To_Socket_Address()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);
        var context = MakeContext("/api/identity/login", remoteIp: "10.0.0.1");

        await middleware.InvokeAsync(context);

        Assert.Equal("auth:10.0.0.1", limiter.LastKey);
    }

    /// <summary>
    /// Proxy'siz yayında (<c>TrustedProxyHopCount = 0</c>) başlık hiç dikkate alınmaz.
    /// </summary>
    [Fact]
    public async Task Zero_Trusted_Hops_Ignores_The_Header()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask, trustedProxyHopCount: 0);
        var context = MakeContext("/api/identity/login", forwardedFor: "9.9.9.9", remoteIp: "10.0.0.1");

        await middleware.InvokeAsync(context);

        Assert.Equal("auth:10.0.0.1", limiter.LastKey);
    }

    /// <summary>
    /// M16-1: Mesaj gönderimi kendi (sıkı) politikasına düşer ve partition <b>kullanıcı</b> kimliğidir —
    /// IP'ye göre sayılsaydı aynı ev/okul ağındaki iki öğrenci birbirinin mesaj kotasını tüketirdi.
    /// </summary>
    [Fact]
    public async Task Send_Message_Uses_Messaging_Policy_Partitioned_By_User()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);
        var userId = "8f0d2a1c-0000-0000-0000-000000000001";
        var context = MakeContext(
            $"/api/messaging/conversations/{Guid.NewGuid()}/messages", method: "POST", userId: userId);

        await middleware.InvokeAsync(context);

        Assert.Equal($"messaging:{userId}", limiter.LastKey);
    }

    [Fact]
    public async Task Send_Message_Over_The_Limit_Returns_429()
    {
        var limiter = new FakeRateLimiter(allow: false);
        var nextCalled = false;
        var middleware = Build(limiter, _ => { nextCalled = true; return Task.CompletedTask; });
        var context = MakeContext(
            $"/api/messaging/conversations/{Guid.NewGuid()}/messages", method: "POST", userId: "u-1");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
    }

    /// <summary>Gelen kutusunu okumak mesaj göndermek değildir: sıkı kotaya girmez.</summary>
    [Fact]
    public async Task Reading_Messages_Falls_Back_To_Default_Policy()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);
        var context = MakeContext(
            $"/api/messaging/conversations/{Guid.NewGuid()}/messages", method: "GET", userId: "u-1");

        await middleware.InvokeAsync(context);

        Assert.StartsWith("default:", limiter.LastKey);
    }

    /// <summary>Okundu işaretleme de POST'tur ama mesaj göndermez; sıkı kotaya girmemeli.</summary>
    [Fact]
    public async Task Marking_Read_Falls_Back_To_Default_Policy()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);
        var context = MakeContext(
            $"/api/messaging/conversations/{Guid.NewGuid()}/read", method: "POST", userId: "u-1");

        await middleware.InvokeAsync(context);

        Assert.StartsWith("default:", limiter.LastKey);
    }

    /// <summary>Oturum kimliği yoksa mesajlaşma kotası IP'ye düşer — kota yine de uygulanır.</summary>
    [Fact]
    public async Task Anonymous_Send_Falls_Back_To_Client_Ip_Partition()
    {
        var limiter = new FakeRateLimiter(allow: true);
        var middleware = Build(limiter, _ => Task.CompletedTask);
        var context = MakeContext(
            $"/api/messaging/conversations/{Guid.NewGuid()}/messages", method: "POST");

        await middleware.InvokeAsync(context);

        Assert.Equal("messaging:1.2.3.4", limiter.LastKey);
    }

    private static DistributedRateLimitMiddleware Build(
        IRateLimiter limiter,
        RequestDelegate next,
        int trustedProxyHopCount = 1)
        => new(next, limiter, Options.Create(new RateLimitOptions { TrustedProxyHopCount = trustedProxyHopCount }));

    private static DefaultHttpContext MakeContext(
        string path,
        string? forwardedFor = null,
        string remoteIp = "1.2.3.4",
        string method = "GET",
        string? userId = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Request.Path = path;
        context.Request.Method = method;
        if (userId is not null)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"));
        }

        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        if (forwardedFor is not null)
        {
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        }

        return context;
    }

    private sealed class FakeRateLimiter(bool allow) : IRateLimiter
    {
        public int Calls { get; private set; }

        public string? LastKey { get; private set; }

        public int LastPermitLimit { get; private set; }

        public Task<bool> TryAcquireAsync(string partitionKey, int permitLimit, TimeSpan window, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastKey = partitionKey;
            LastPermitLimit = permitLimit;
            return Task.FromResult(allow);
        }
    }
}
