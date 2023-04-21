using System.Security.Claims;
using LayeredTemplate.Infrastructure.Extensions;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Shared.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Infrastructure.Services;

internal class AppClaimTransformation : IClaimsTransformation
{
    private readonly ILogger<AppClaimTransformation> logger;

    public AppClaimTransformation(ILogger<AppClaimTransformation> logger)
    {
        this.logger = logger;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        switch (principal.Identity?.AuthenticationType)
        {
            case AppAuthenticationTypes.User:
            {
                var targetIdentity = principal.Identities.First(x => x.AuthenticationType == principal.Identity?.AuthenticationType);
                var claims = new List<Claim>();
                claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.UserId, AppClaims.UserId));
                claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.Email, AppClaims.Email));
                claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.Phone, AppClaims.Phone));
                claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.Role, AppClaims.Role));
                claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.EmailVerified, AppClaims.EmailVerified));
                claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.PhoneNumberVerified, AppClaims.PhoneNumberVerified));
                claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.NameKey, AppClaims.NameKey));
                claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.FirstNameKey, AppClaims.FirstNameKey));
                claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.LastNameKey, AppClaims.LastNameKey));

                var identity = new ClaimsIdentity(claims, principal.Identity.AuthenticationType);
                var result = new ClaimsPrincipal(identity);

                return Task.FromResult(result);
            }

            case AppAuthenticationTypes.ApiKey:
            {
                return Task.FromResult(principal);
            }

            default:
            {
                return Task.FromResult(principal);
            }
        }
    }
}