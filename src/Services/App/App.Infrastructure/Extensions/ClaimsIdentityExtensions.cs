using System.Security.Claims;

namespace LayeredTemplate.App.Infrastructure.Extensions;

internal static class ClaimsIdentityExtensions
{
    public static Claim? FindAndConvertClaim(this ClaimsIdentity identity, string oldClaimType, string newClaimType)
    {
        var value = identity.FindFirst(oldClaimType)?.Value;

        return value != null
            ? new Claim(newClaimType, value)
            : null;
    }

    public static Claim? CreateClaimIfNotNull(this string? value, string claimType)
    {
        if (value != null)
        {
            return new Claim(claimType, value);
        }

        return null;
    }
}