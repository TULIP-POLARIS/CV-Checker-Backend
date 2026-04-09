using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CVApi.Auth;

internal static class HttpUser
{
    public static Guid? GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
