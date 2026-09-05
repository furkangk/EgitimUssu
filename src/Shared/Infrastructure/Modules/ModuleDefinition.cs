using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Shared.Infrastructure.Modules;

public abstract class ModuleDefinition : IModule
{
    public abstract string Name { get; }

    public abstract string RoutePrefix { get; }

    public abstract void RegisterServices(IServiceCollection services, IConfiguration configuration);

    public abstract void MapEndpoints(IEndpointRouteBuilder endpoints);

    /// <summary>
    /// A-13: Modülün uç grubunu <b>varsayılan-deny</b> olarak açar — grup baştan
    /// <c>AuthenticatedUser</c> politikasına tabidir.
    /// <para>
    /// Eskiden grup korumasızdı ve her modülün <c>RequireAuthorization</c>'ı kendisi çağırması
    /// gerekiyordu; koruma bir satırı hatırlamaya bağlıydı. CQRS katmanındaki
    /// <c>AuthorizationCoverageValidator</c> güçlü bir emniyet ağıdır ama yalnız dispatcher'dan
    /// geçen uçları görür — doğrudan yanıt üreten bir uç (ör. dosya indirme, durum ucu) o ağa hiç
    /// takılmaz. Varsayılanı ters çevirmek, unutmanın sonucunu "sessizce açık" yerine "kapalı" yapar.
    /// </para>
    /// <para>
    /// Anonim erişmesi gereken uçlar bunu <b>açıkça</b> söyler: <c>.AllowAnonymous()</c>.
    /// </para>
    /// </summary>
    /// <param name="endpoints">Uçların ekleneceği yönlendirme oluşturucusu.</param>
    /// <returns>Modülün kimlik doğrulaması zorunlu uç grubu.</returns>
    protected RouteGroupBuilder CreateModuleGroup(IEndpointRouteBuilder endpoints)
    {
        return endpoints
            .MapGroup(RoutePrefix)
            .RequireAuthorization(AuthenticatedUserPolicy);
    }

    /// <summary>Kimlik doğrulanmış kullanıcı politikasının adı.</summary>
    public const string AuthenticatedUserPolicy = "AuthenticatedUser";
}
