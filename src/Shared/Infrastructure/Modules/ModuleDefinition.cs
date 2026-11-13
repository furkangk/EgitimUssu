using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Shared.Infrastructure.Modules;

public abstract class ModuleDefinition : IModule
{
    /// <summary>Kimlik doğrulanmış kullanıcı politikasının adı.</summary>
    public const string AuthenticatedUserPolicy = "AuthenticatedUser";

    public abstract string Name { get; }

    public abstract string RoutePrefix { get; }

    /// <summary>
    /// C-10: Modülün uçlarının uyduğu sözleşme sürümü. Bir modül kırıcı bir değişiklik yapmak
    /// istediğinde <c>/api/v2/...</c> altında <b>ikinci</b> bir grup açar (bkz.
    /// <c>CreateModuleGroup(endpoints, prefix, version)</c>); bu özellik varsayılan grubun sürümüdür.
    /// </summary>
    protected virtual string ApiVersion => ApiVersions.Current;

    public abstract void RegisterServices(IServiceCollection services, IConfiguration configuration);

    public abstract void MapEndpoints(IEndpointRouteBuilder endpoints);

    protected RouteGroupBuilder CreateModuleGroup(IEndpointRouteBuilder endpoints)
    {
        return CreateModuleGroup(endpoints, RoutePrefix, ApiVersion);
    }

    /// <summary>
    /// Verilen yol ön eki ve sözleşme sürümüyle bir uç grubu açar. Sürüm etiketi grubun
    /// metadata'sına yazılır: <c>X-Api-Version</c> başlığı ve OpenAPI etiketi bunu okur.
    /// </summary>
    protected RouteGroupBuilder CreateModuleGroup(IEndpointRouteBuilder endpoints, string routePrefix, string apiVersion)
    {
        return endpoints
            .MapGroup(routePrefix)
            .WithApiVersion(apiVersion)
            .RequireAuthorization(AuthenticatedUserPolicy);
    }
}
