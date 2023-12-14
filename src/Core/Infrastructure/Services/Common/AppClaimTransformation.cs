using System.Security.Claims;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Infrastructure.Extensions;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Shared.Extensions;
using Microsoft.AspNetCore.Authentication;

namespace LayeredTemplate.Infrastructure.Services.Common;

internal class AppClaimTransformation : IClaimsTransformation
{
    private readonly IApplicationDbContext context;

    public AppClaimTransformation(IApplicationDbContext context)
    {
        this.context = context;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ClaimsPrincipal result;
        switch (principal.Identity?.AuthenticationType)
        {
            case AppAuthenticationSchemes.Bearer:
            {
                var targetIdentity = principal.Identities.First(x => x.AuthenticationType == principal.Identity?.AuthenticationType);

                var userId = Guid.Parse(targetIdentity.FindFirst(TokenKeys.UserId)!.Value);
                var user = await this.context.Users.FindAsync(userId);
                if (user == null)
                {
                    var claims = new List<Claim>();
                    claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.UserId, AppClaims.UserId));
                    claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.Email, AppClaims.Email));
                    claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.Phone, AppClaims.Phone));
                    claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.Role, AppClaims.Role));
                    claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.EmailVerified, AppClaims.EmailVerified));
                    claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.PhoneVerified, AppClaims.PhoneVerified));
                    claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.NameKey, AppClaims.Name));
                    claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.FirstNameKey, AppClaims.FirstName));
                    claims.AddIfNotNull(targetIdentity.FindAndConvertClaim(TokenKeys.LastNameKey, AppClaims.LastName));

                    var identity = new ClaimsIdentity(claims, principal.Identity.AuthenticationType);
                    result = new ClaimsPrincipal(identity);
                }
                else
                {
                    var claims = new List<Claim>();
                    claims.AddIfNotNull(userId.ToString().CreateClaimIfNotNull(AppClaims.UserId));
                    claims.AddIfNotNull(user.Email.CreateClaimIfNotNull(AppClaims.Email));
                    claims.AddIfNotNull(user.Phone.CreateClaimIfNotNull(AppClaims.Phone));
                    claims.AddIfNotNull(user.Role.ToString().CreateClaimIfNotNull(AppClaims.Role));
                    claims.AddIfNotNull(user.EmailVerified.ToString().ToLower().CreateClaimIfNotNull(AppClaims.EmailVerified));
                    claims.AddIfNotNull(user.PhoneVerified.ToString().ToLower().CreateClaimIfNotNull(AppClaims.PhoneVerified));
                    claims.AddIfNotNull($"{user.FirstName} {user.LastName}".Trim().CreateClaimIfNotNull(AppClaims.Name));
                    claims.AddIfNotNull(user.FirstName.CreateClaimIfNotNull(AppClaims.FirstName));
                    claims.AddIfNotNull(user.LastName.CreateClaimIfNotNull(AppClaims.LastName));

                    var identity = new ClaimsIdentity(claims, principal.Identity.AuthenticationType);
                    result = new ClaimsPrincipal(identity);
                }

                break;
            }

            case AppAuthenticationSchemes.ApiKey:
            {
                result = principal.Clone();
                break;
            }

            default:
            {
                result = principal.Clone();
                break;
            }
        }

        return result;
    }
}