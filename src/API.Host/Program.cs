using System.Reflection;
using System.Threading.RateLimiting;
using System.Text;
using EgitimUssu.API.Host;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Extensions;
using EgitimUssu.Shared.Infrastructure.Health;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Middleware;
using EgitimUssu.Shared.Infrastructure.Modules;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddSharedInfrastructure(builder.Configuration);
builder.Services.AddDiscoveredModules(builder.Configuration, ModuleAssemblies.All);
builder.Services
    .AddHealthChecks()
    .AddCheck<ConfigurationHealthCheck>("configuration", tags: ["ready"])
    .AddCheck<DatabaseConnectionHealthCheck>("database", tags: ["ready"]);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
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
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("default", limiter =>
    {
        limiter.PermitLimit = 120;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.OnRejected = async (context, token) =>
    {
        if (!context.HttpContext.Response.HasStarted)
        {
            var result = ApiErrorHttpResults.FromError(
                context.HttpContext,
                StatusCodes.Status429TooManyRequests,
                new EgitimUssu.Shared.Kernel.Error("shared.rate_limited", "Too many requests. Please try again later."));
            await result.ExecuteAsync(context.HttpContext);
        }
        await Task.CompletedTask;
    };
});

var app = builder.Build();

app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
app.UseMiddleware<RequestContextLoggingMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.MapGet("/api/meta/version", (IReadOnlyCollection<IModule> modules) => Results.Ok(new
{
    service = "EgitimUssu.API.Host",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
    modules = modules.Select(module => new { module.Name, module.RoutePrefix })
}));

app.MapDiscoveredModules();

var databaseOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;
if (databaseOptions.ApplyMigrationsOnStartup)
{
    await app.Services.ApplyModuleMigrationsAsync();
}

await app.RunAsync();

public partial class Program;
