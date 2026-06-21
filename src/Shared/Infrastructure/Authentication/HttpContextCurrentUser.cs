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

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}
