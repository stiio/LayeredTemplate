using System.Security.Claims;

namespace LayeredTemplate.Infrastructure.Extensions;

internal static class ClaimsIdentityExtensions
{
    public static Claim? FindAndConvertClaim(this ClaimsIdentity identity, string oldClaimType, string newClaimType)
    {
        var value = identity.FindFirst(oldClaimType)?.Value;

        return value != null
            ? new Claim(newClaimType, value)
            : null;
    }
}