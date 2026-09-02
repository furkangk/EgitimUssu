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
using EgitimUssu.Shared.Infrastructure.Modules;
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

app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
app.UseMiddleware<RequestContextLoggingMiddleware>();
// Y4: Redis destekli dağıtık, partition'lı rate limiting (fail-open). Yol tabanlı politika.
app.UseMiddleware<DistributedRateLimitMiddleware>();
app.UseAuthentication();
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
}

await app.RunAsync();

public partial class Program
{
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
