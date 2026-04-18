using System.Security.Claims;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LayeredTemplate.Auth.Web.Controllers;

public class ConnectController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = this.HttpContext.GetOpenIddictServerRequest() ??
                      throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var user = await userManager.GetUserAsync(this.User);
        if (user is null)
        {
            var returnUrl = this.Request.PathBase + this.Request.Path + QueryString.Create(
                this.Request.HasFormContentType ? this.Request.Form.ToList() : this.Request.Query.ToList());

            return this.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                IdentityConstants.ApplicationScheme);
        }

        var identity = await this.BuildUserIdentityAsync(user);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetDestinations(ResolveDestinations);

        return this.SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange()
    {
        var request = this.HttpContext.GetOpenIddictServerRequest() ??
                      throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsClientCredentialsGrantType())
        {
            // OpenIddict has already validated client_id + client_secret and that the requested
            // scopes are a subset of what the client is permitted. We just build the principal.
            var identity = new ClaimsIdentity(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                Claims.Name,
                Claims.Role);

            identity.AddClaim(new Claim(Claims.Subject, request.ClientId!)
                .SetDestinations(Destinations.AccessToken));

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(request.GetScopes());

            return this.SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await this.HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var userId = result.Principal?.GetClaim(Claims.Subject);

            var user = await userManager.FindByIdAsync(userId!);
            if (user is null)
            {
                return this.Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user no longer exists.",
                    }));
            }

            var identity = await this.BuildUserIdentityAsync(user);
            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(result.Principal!.GetScopes());
            principal.SetDestinations(ResolveDestinations);

            return this.SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return this.Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.UnsupportedGrantType,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The specified grant type is not supported.",
            }));
    }

    [HttpGet("~/connect/userinfo")]
    public async Task<IActionResult> Userinfo()
    {
        var result = await this.HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var userId = result.Principal?.GetClaim(Claims.Subject);

        var user = await userManager.FindByIdAsync(userId!);
        if (user is null)
        {
            return this.Challenge(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user no longer exists.",
                }));
        }

        var principal = result.Principal!;
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = userId!,
        };

        if (principal.HasScope(Scopes.Profile))
        {
            var userName = await userManager.GetUserNameAsync(user);
            if (!string.IsNullOrEmpty(userName))
            {
                claims[Claims.Name] = userName;
            }

            if (!string.IsNullOrEmpty(user.FirstName))
            {
                claims[Claims.GivenName] = user.FirstName;
            }

            if (!string.IsNullOrEmpty(user.LastName))
            {
                claims[Claims.FamilyName] = user.LastName;
            }
        }

        if (principal.HasScope(Scopes.Email))
        {
            var email = await userManager.GetEmailAsync(user);
            if (!string.IsNullOrEmpty(email))
            {
                claims[Claims.Email] = email;
            }

            claims[Claims.EmailVerified] = user.EmailConfirmed;
        }

        if (principal.HasScope(Scopes.Phone))
        {
            if (!string.IsNullOrEmpty(user.PhoneNumber))
            {
                claims[Claims.PhoneNumber] = user.PhoneNumber;
            }

            claims[Claims.PhoneNumberVerified] = user.PhoneNumberConfirmed;
        }

        if (principal.HasScope(AppScopes.Roles))
        {
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Count > 0)
            {
                claims[Claims.Role] = roles.ToArray();
            }
        }

        return this.Ok(claims);
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();

        return this.SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }

    /// <summary>
    /// Assembles a <see cref="ClaimsIdentity"/> for the given user with all claims the provider
    /// may emit. Which claims end up in which token is decided later by <see cref="ResolveDestinations"/>
    /// based on the scopes granted for the principal.
    /// </summary>
    private async Task<ClaimsIdentity> BuildUserIdentityAsync(ApplicationUser user)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name,
            Claims.Role);

        identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user));
        identity.SetClaim(Claims.Name, await userManager.GetUserNameAsync(user));
        identity.SetClaim(Claims.GivenName, user.FirstName);
        identity.SetClaim(Claims.FamilyName, user.LastName);
        identity.SetClaim(Claims.Email, await userManager.GetEmailAsync(user));
        identity.SetClaim(Claims.EmailVerified, user.EmailConfirmed);
        identity.SetClaim(Claims.PhoneNumber, user.PhoneNumber);
        identity.SetClaim(Claims.PhoneNumberVerified, user.PhoneNumberConfirmed);

        foreach (var role in await userManager.GetRolesAsync(user))
        {
            identity.AddClaim(new Claim(Claims.Role, role));
        }

        return identity;
    }

    /// <summary>
    /// Decides which tokens each claim ends up in. Access token always carries identity claims
    /// (so downstream APIs can authorize), id_token only carries claims for scopes the client
    /// actually requested — per OIDC Core §5.4.
    /// </summary>
    private static IEnumerable<string> ResolveDestinations(Claim claim) => claim.Type switch
    {
        Claims.Subject =>
            [Destinations.AccessToken, Destinations.IdentityToken],

        Claims.Name or Claims.GivenName or Claims.FamilyName =>
            WithIdTokenIfScoped(claim, Scopes.Profile),

        Claims.Email or Claims.EmailVerified =>
            WithIdTokenIfScoped(claim, Scopes.Email),

        Claims.PhoneNumber or Claims.PhoneNumberVerified =>
            WithIdTokenIfScoped(claim, Scopes.Phone),

        Claims.Role =>
            WithIdTokenIfScoped(claim, AppScopes.Roles),

        _ => [Destinations.AccessToken],
    };

    private static IEnumerable<string> WithIdTokenIfScoped(Claim claim, string scope) =>
        claim.Subject!.HasScope(scope)
            ? [Destinations.AccessToken, Destinations.IdentityToken]
            : [Destinations.AccessToken];
}
