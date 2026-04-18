using System.Security.Claims;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.OpenIddict;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LayeredTemplate.Auth.Web.Controllers;

public class ConnectController : Controller
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly SignInManager<ApplicationUser> signInManager;
    private readonly IOpenIddictScopeManager scopeManager;

    public ConnectController(UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager)
    {
        this.userManager = userManager;
        this.signInManager = signInManager;
        this.scopeManager = scopeManager;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = this.HttpContext.GetOpenIddictServerRequest() ??
                      throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var user = await this.userManager.GetUserAsync(this.User);
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
        await this.AttachResourcesAsync(principal);
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
            await this.AttachResourcesAsync(principal);

            return this.SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await this.HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var userId = result.Principal?.GetClaim(Claims.Subject);

            var user = await this.userManager.FindByIdAsync(userId!);
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
            await this.AttachResourcesAsync(principal);
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

        var user = await this.userManager.FindByIdAsync(userId!);
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
            var userName = await this.userManager.GetUserNameAsync(user);
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
            var email = await this.userManager.GetEmailAsync(user);
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
            var roles = await this.userManager.GetRolesAsync(user);
            if (roles.Count > 0)
            {
                claims[Claims.Role] = roles.ToArray();
            }
        }

        return this.Ok(claims);
    }

    [HttpGet("~/connect/logout")]
    public async Task<IActionResult> LogoutGet()
    {
        var request = this.HttpContext.GetOpenIddictServerRequest() ??
                      throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // If the RP passed post_logout_redirect_uri, OpenIddict has already validated it against
        // the client's registered logout URIs (and validated id_token_hint if present). That's
        // enough to treat the request as legitimate — skip confirmation and log out silently.
        if (!string.IsNullOrEmpty(request.PostLogoutRedirectUri))
        {
            return await this.PerformLogoutAsync();
        }

        // No trust signal → show confirmation page to prevent nuisance CSRF
        // (e.g. <img src="/connect/logout"> on an attacker's site).
        return this.Redirect($"/account/logout_confirmation{this.HttpContext.Request.QueryString}");
    }

    [HttpPost("~/connect/logout")]
    public Task<IActionResult> LogoutPost() => this.PerformLogoutAsync();

    private async Task<IActionResult> PerformLogoutAsync()
    {
        await this.signInManager.SignOutAsync();

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

        identity.SetClaim(Claims.Subject, await this.userManager.GetUserIdAsync(user));
        identity.SetClaim(Claims.Name, await this.userManager.GetUserNameAsync(user));
        identity.SetClaim(Claims.GivenName, user.FirstName);
        identity.SetClaim(Claims.FamilyName, user.LastName);
        identity.SetClaim(Claims.Email, await this.userManager.GetEmailAsync(user));
        identity.SetClaim(Claims.EmailVerified, user.EmailConfirmed);
        identity.SetClaim(Claims.PhoneNumber, user.PhoneNumber);
        identity.SetClaim(Claims.PhoneNumberVerified, user.PhoneNumberConfirmed);

        foreach (var role in await this.userManager.GetRolesAsync(user))
        {
            identity.AddClaim(new Claim(Claims.Role, role));
        }

        return identity;
    }

    /// <summary>
    /// Reads the resources (API URIs) registered for each granted scope and attaches them to the
    /// principal. OpenIddict uses these to populate <c>aud</c> in the access_token — resource
    /// servers validate <c>aud</c> against their own identifier (<c>api://...</c>) to reject
    /// tokens meant for other services. Without this call, <c>aud</c> is omitted entirely.
    /// </summary>
    private async Task AttachResourcesAsync(ClaimsPrincipal principal)
    {
        var resources = await this.scopeManager
            .ListResourcesAsync(principal.GetScopes())
            .ToListAsync();

        principal.SetResources(resources);
    }

    /// <summary>
    /// Decides which tokens each claim ends up in.
    /// Per OIDC Core §5.4: identity claims are emitted only when the corresponding scope was
    /// granted. When it is, we include the claim in BOTH access_token and id_token — so backends
    /// can authorize on them without a /connect/userinfo roundtrip, and SPAs can show them in UI.
    /// <para>
    /// This keeps access_token lean: client that asked only for <c>openid</c> gets an AT with
    /// just <c>sub</c>; asking for <c>profile email roles</c> adds only those claim families.
    /// </para>
    /// </summary>
    private static IEnumerable<string> ResolveDestinations(Claim claim) => claim.Type switch
    {
        // sub is the stable identifier — always in both tokens (required by OIDC Core §2).
        Claims.Subject =>
            [Destinations.AccessToken, Destinations.IdentityToken],

        Claims.Name or Claims.GivenName or Claims.FamilyName
            when claim.Subject!.HasScope(Scopes.Profile)
            => [Destinations.AccessToken, Destinations.IdentityToken],

        Claims.Email or Claims.EmailVerified
            when claim.Subject!.HasScope(Scopes.Email)
            => [Destinations.AccessToken, Destinations.IdentityToken],

        Claims.PhoneNumber or Claims.PhoneNumberVerified
            when claim.Subject!.HasScope(Scopes.Phone)
            => [Destinations.AccessToken, Destinations.IdentityToken],

        Claims.Role when claim.Subject!.HasScope(AppScopes.Roles)
            => [Destinations.AccessToken, Destinations.IdentityToken],

        // Unknown / ungranted — don't emit to either token.
        _ => [],
    };
}
