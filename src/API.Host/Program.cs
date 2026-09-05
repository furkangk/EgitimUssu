using System.IdentityModel.Tokens.Jwt;
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
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddProblemDetails();
// C-10: OpenAPI belgesi yayınlanan **sözleşme** sürümünü taşır (derleme sürümünü değil) —
// belgeyi indiren istemci hangi sözleşmeye baktığını `X-Api-Version` başlığıyla aynı jetondan okur.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info ??= new OpenApiInfo();
    document.Info.Title = "EğitimÜssü API";
    document.Info.Version = ApiVersions.Current;
    document.Info.Description =
        "Sözleşme sürümü " + ApiVersions.Current + ". Her yanıt `X-Api-Version` başlığını taşır. "
        + "Kırıcı değişiklikler yeni bir sürüm yolu (`/api/v2/...`) altında açılır; "
        + "mevcut yollar kırılmaz. Politika: doc/architecture/api_versioning.md";
    return Task.CompletedTask;
}));
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
    .AddCheck<DatabaseConnectionHealthCheck>("database", tags: ["ready"]);

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
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrEmpty(jti))
                {
                    var blacklist = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklist>();
                    if (await blacklist.IsBlacklistedAsync(jti, context.HttpContext.RequestAborted))
                    {
                        context.Fail("Token has been revoked.");
                    }
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
// C-10: Sürüm başlığı olabildiğince dışta durur — 401/403/429 ve beklenmeyen 500 dahil **her**
// /api/* yanıtı sözleşme sürümünü taşısın. Exception middleware'in içinde kalır ki yakalanmış bir
// hatanın yanıtı da başlığı korusun (o middleware yalnız durum+gövde yazar, başlıkları temizlemez).
app.UseMiddleware<ApiVersionHeaderMiddleware>();
app.UseMiddleware<RequestContextLoggingMiddleware>();
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
app.MapOpenApi();

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
    /// API host sürümünü ve çalışma zamanında keşfedilen modüllerin ad ile route prefix bilgilerini döndürür.
    /// <para>
    /// C-10: <c>apiVersion</c> yayınlanan <b>sözleşmenin</b> sürümüdür ve <c>X-Api-Version</c>
    /// başlığıyla aynı değerdir; <c>version</c> ise çalışan <b>derlemenin</b> sürümüdür. İkisi ayrı
    /// tempoda değişir: derleme her yayında, sözleşme yalnız kırıcı değişiklikte.
    /// </para>
    /// </summary>
    private static IResult GetApiVersion(IReadOnlyCollection<IModule> modules)
    {
        return Results.Ok(new
        {
            service = "EgitimUssu.API.Host",
            apiVersion = ApiVersions.Current,
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            modules = modules.Select(module => new { module.Name, module.RoutePrefix })
        });
    }
}
