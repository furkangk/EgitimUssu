using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Reflection;
using System.Text;
using EgitimUssu.API.Host;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Caching;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Extensions;
using EgitimUssu.Shared.Infrastructure.Health;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Middleware;
using EgitimUssu.Modules.Membership.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Infrastructure.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddSharedInfrastructure(builder.Configuration);
// C-04/K-10: Gözlemlenebilirlik. `Observability:Enabled=false` iken (varsayılan) hiçbir exporter
// kurulmaz — testte ve CI'da maliyet sıfırdır; `EgitimUssuMetrics` yine de kaydedilir, çünkü iş
// kodu ona her ortamda bağımlıdır.
builder.Services.AddObservability(builder.Configuration, serviceName: "egitimussu-api");
builder.Services.AddDiscoveredModules(builder.Configuration, ModuleAssemblies.All);
builder.Services.ValidateAuthorizationCoverage();
builder.Services
    .AddHealthChecks()
    .AddCheck<ConfigurationHealthCheck>("configuration", tags: ["ready"])
    .AddCheck<DatabaseConnectionHealthCheck>("database", tags: ["ready"])
    // A-01: Redis'e bağlı korumalar fail-open olduğu için kesinti hiçbir isteği hata döndürmez;
    // erişilebilirliğin dışarıdan görülebildiği tek yer bu kontroldür (sonucu Degraded — fail-open
    // bilinçli bir karar olduğundan Redis kesintisi uygulamayı yeniden başlatmaya değmez).
    .AddCheck<RedisConnectionHealthCheck>("redis", tags: ["ready"]);

// A-17: Tarayıcı istemcileri için CORS. Liste boşken (bugünkü mobil-only durum) hiçbir köken açılmaz.
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
if (corsOptions.HasOrigins)
{
    builder.Services.AddCors(options => options.AddPolicy(
        CorsOptions.PolicyName,
        policy => policy
            .WithOrigins(corsOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
}

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
// Y3: Zayıf/eksik/yer-tutucu imzalama anahtarını startup'ta reddet (fail-fast).
JwtSigningKeyGuard.EnsureValid(jwtOptions.SigningKey);
// A-06: Bağlantı dizesi repoda tutulmaz; boş/zayıf parolalı dizeyi startup'ta reddet (fail-fast).
ConnectionStringGuard.EnsureValid(
    builder.Configuration.GetConnectionString("Postgres"),
    builder.Environment.IsDevelopment());
// P02/K-01: E-posta sağlayıcısı eksik yapılandırılmışsa uygulama açılmasın (sessizce e-posta göndermeme yasak).
var emailOptions = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
EmailOptionsGuard.EnsureValid(emailOptions, builder.Environment.IsDevelopment());
// P03/K-02: Push sağlayıcısı eksik yapılandırılmışsa uygulama açılmasın (sessizce push göndermeme yasak).
var pushOptions = builder.Configuration.GetSection(PushOptions.SectionName).Get<PushOptions>() ?? new PushOptions();
PushOptionsGuard.EnsureValid(pushOptions, builder.Environment.IsDevelopment());
// P04/K-03: Depolama sağlayıcısı eksik yapılandırılmışsa uygulama açılmasın (yüklenen dosya kaybolmasın).
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
StorageOptionsGuard.EnsureValid(storageOptions, builder.Environment.IsDevelopment());
// A-01: Redis eksikse hız sınırı, token kara listesi, giriş kilidi ve idempotency **sessizce** kapanır.
// Diğer sırlarla aynı muamele: üretimde eksik/loopback adresle uygulama açılmaz.
var redisOptions = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
RedisOptionsGuard.EnsureValid(redisOptions, builder.Environment.IsDevelopment());
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            // Y4: Token blacklist — iptal edilmiş (logout'lanmış) erişim token'ı reddedilir (anlık iptal).
            // A-06: Buna ek olarak kullanıcı bazlı iptal damgası denetlenir: parola sıfırlama ve rol
            // değişimi tek tek `jti` bilemez, "şu andan önce verilen token'lar geçersiz" der. İki
            // kontrol tek Redis turunda yapılır.
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var jti = principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                var userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var issuedAtUtc = ReadIssuedAtUtc(principal);

                if (string.IsNullOrEmpty(jti) && (userId is null || issuedAtUtc is null))
                {
                    return;
                }

                var blacklist = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklist>();
                if (await blacklist.IsRevokedAsync(jti ?? string.Empty, userId, issuedAtUtc, context.HttpContext.RequestAborted))
                {
                    context.Fail("Token has been revoked.");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (!context.Response.HasStarted)
                {
                    var result = ApiErrorHttpResults.Unauthorized(
                        context.HttpContext,
                        "Authentication is required to access this resource.");
                    await result.ExecuteAsync(context.HttpContext);
                }
            },
            OnForbidden = async context =>
            {
                if (!context.Response.HasStarted)
                {
                    var result = ApiErrorHttpResults.Forbidden(
                        context.HttpContext,
                        "You do not have permission to access this resource.");
                    await result.ExecuteAsync(context.HttpContext);
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
});

var app = builder.Build();

// Üretimde "Logging" sağlayıcısı bilinçli bir geçiş dönemi seçimidir; sessiz kalmasın diye uyarılır.
if (!app.Environment.IsDevelopment()
    && string.Equals(emailOptions.Provider, EmailOptions.LoggingProvider, StringComparison.OrdinalIgnoreCase))
{
    app.Logger.LogWarning(
        "Email:Provider = \"Logging\" — e-postalar GÖNDERİLMİYOR, yalnız log'a yazılıyor. Üretimde Smtp'ye geçin.");
}

if (!app.Environment.IsDevelopment()
    && string.Equals(pushOptions.Provider, PushOptions.LoggingProvider, StringComparison.OrdinalIgnoreCase))
{
    app.Logger.LogWarning(
        "Push:Provider = \"Logging\" — push bildirimleri GÖNDERİLMİYOR, yalnız log'a yazılıyor. Üretimde Fcm'ye geçin.");
}

app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
// A-17: Güvenlik başlıkları her yanıta yazılır — hata ve 429 yanıtları dahil, bu yüzden
// rate limiting ve yetkilendirmeden ÖNCE gelir.
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestContextLoggingMiddleware>();
if (corsOptions.HasOrigins)
{
    // Kimlik doğrulamadan önce: preflight (OPTIONS) isteği Authorization başlığı taşımaz.
    app.UseCors(CorsOptions.PolicyName);
}
// M16-1: Kimlik doğrulama rate limiting'ten ÖNCE gelir. `UseAuthentication` yalnız token'ı çözüp
// `HttpContext.User`'ı doldurur (hiçbir isteği reddetmez); mesajlaşma kotası kullanıcı başına
// sayıldığı için limitleyicinin bu kimliği görmesi şart. Sırası tersken partition sessizce IP'ye
// düşüyordu — aynı ev/okul ağındaki kullanıcılar birbirinin kotasını tüketirdi.
app.UseAuthentication();
// Y4: Redis destekli dağıtık, partition'lı rate limiting (fail-open). Yol tabanlı politika.
app.UseMiddleware<DistributedRateLimitMiddleware>();
app.UseAuthorization();
// Y4: Idempotency-Key ile mutasyon uçlarında tekrarlı istek koruması (auth sonrası — kullanıcıya göre kapsam).
app.UseMiddleware<IdempotencyMiddleware>();

// A-14: OpenAPI belgesi yalnız geliştirmede yayınlanır. Kimliksiz erişilebilen `/openapi/v1.json`
// yönetici uçları dahil tüm API yüzeyini — yol, şema ve alan adlarıyla — dışarı veriyordu.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.MapGet("/api/meta/version", GetApiVersion)
.WithSummary("API sürüm ve modül bilgilerini getirir");

app.MapDiscoveredModules();
app.MapTeacherDashboard();

var databaseOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;
if (databaseOptions.ApplyMigrationsOnStartup)
{
    await app.Services.ApplyModuleMigrationsAsync();

    // M17-1: Plan kataloğu tohumlaması YALNIZ Development'ta koşar — fiyatlar henüz hipotez (Q2).
    // Üretimde planlar Admin ucundan (POST /api/membership/plans) girilir.
    if (app.Environment.IsDevelopment())
    {
        await app.Services.SeedMembershipPlansAsync();
    }
}

await app.RunAsync();

public partial class Program
{
    /// <summary>
    /// A-06: Token'ın <c>iat</c> claim'ini UTC'ye çevirir. Değer Unix saniyesidir; okunamıyorsa
    /// <c>null</c> döner ve kullanıcı bazlı iptal damgası denetlenmez (yalnız <c>jti</c> kara listesi kalır).
    /// </summary>
    private static DateTime? ReadIssuedAtUtc(ClaimsPrincipal? principal)
    {
        var raw = principal?.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
        return long.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;
    }

    /// <summary>
    /// API host sürümünü ve çalışma zamanında keşfedilen modüllerin ad ile route prefix bilgilerini döndürür.
    /// </summary>
    private static IResult GetApiVersion(IReadOnlyCollection<IModule> modules)
    {
        return Results.Ok(new
        {
            service = "EgitimUssu.API.Host",
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            modules = modules.Select(module => new { module.Name, module.RoutePrefix })
        });
    }
}
