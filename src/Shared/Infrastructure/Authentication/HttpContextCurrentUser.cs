using System.Security.Claims;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Http;

namespace EgitimUssu.Shared.Infrastructure.Authentication;

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray() ?? [];

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <summary>M01-4: Erişim jetonundaki <c>sid</c> talebi — hangi cihaz oturumundan geldiği.</summary>
    public Guid? SessionId =>
        Guid.TryParse(Principal?.FindFirstValue(SessionIdClaimType), out var sessionId) ? sessionId : null;

    /// <summary>JWT'de oturum kimliğini taşıyan talep adı (RFC 7519 "sid").</summary>
    public const string SessionIdClaimType = "sid";

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}
