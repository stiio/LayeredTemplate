using System.ComponentModel.DataAnnotations;
using System.Text;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.Identity;
using LayeredTemplate.Auth.Web.Infrastructure.Options.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class AcceptInvite : ComponentBase
{
    private InviteState state = InviteState.Invalid;
    private string? message;
    private string loginUrl = "account/login";

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private IOptions<CorsSettings> CorsSettings { get; set; } = default!;

    [Inject]
    private ILogger<AcceptInvite> Logger { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? UserId { get; set; }

    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        this.Input ??= new();

        this.loginUrl = string.IsNullOrEmpty(this.ReturnUrl)
            ? "account/login"
            : $"account/login?returnUrl={Uri.EscapeDataString(this.ReturnUrl)}";

        if (string.IsNullOrEmpty(this.UserId) || string.IsNullOrEmpty(this.Code))
        {
            this.state = InviteState.Invalid;
            return;
        }

        var user = await this.UserManager.FindByIdAsync(this.UserId);
        if (user is null)
        {
            this.state = InviteState.Invalid;
            return;
        }

        if (!this.TryDecodeToken(out var decoded))
        {
            this.state = InviteState.Invalid;
            return;
        }

        var valid = await this.UserManager.VerifyUserTokenAsync(
            user,
            InviteTokenSettings.ProviderName,
            InviteTokenSettings.Purpose,
            decoded);

        if (!valid)
        {
            this.state = InviteState.Invalid;
            return;
        }

        var hasPassword = await this.UserManager.HasPasswordAsync(user);
        var logins = await this.UserManager.GetLoginsAsync(user);

        this.state = hasPassword || logins.Count > 0
            ? InviteState.AlreadyActive
            : InviteState.NeedsPassword;
    }

    private async Task OnSetPasswordAsync()
    {
        if (string.IsNullOrEmpty(this.UserId) || string.IsNullOrEmpty(this.Code))
        {
            this.state = InviteState.Invalid;
            return;
        }

        var user = await this.UserManager.FindByIdAsync(this.UserId);
        if (user is null)
        {
            this.state = InviteState.Invalid;
            return;
        }

        if (!this.TryDecodeToken(out var decoded))
        {
            this.state = InviteState.Invalid;
            return;
        }

        var valid = await this.UserManager.VerifyUserTokenAsync(
            user,
            InviteTokenSettings.ProviderName,
            InviteTokenSettings.Purpose,
            decoded);

        if (!valid)
        {
            this.state = InviteState.Invalid;
            return;
        }

        if (await this.UserManager.HasPasswordAsync(user))
        {
            // Race condition: another tab already accepted — treat as already active.
            this.state = InviteState.AlreadyActive;
            return;
        }

        var addPwd = await this.UserManager.AddPasswordAsync(user, this.Input.Password);
        if (!addPwd.Succeeded)
        {
            this.message = $"Error: {string.Join(" ", addPwd.Errors.Select(e => e.Description))}";
            this.state = InviteState.NeedsPassword;
            return;
        }

        // Accepting the invite implicitly confirms the email — the link arrived in that mailbox.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await this.UserManager.UpdateAsync(user);
        }

        this.Logger.LogInformation("User {UserId} accepted invite and set a password.", this.UserId);

        // PasswordSignInAsync detects 2FA (unlikely for a fresh invitee, but correct).
        var signIn = await this.SignInManager.PasswordSignInAsync(
            user,
            this.Input.Password,
            isPersistent: false,
            lockoutOnFailure: false);

        if (signIn.Succeeded)
        {
            this.NavigateAfterInvite();
            return;
        }

        if (signIn.RequiresTwoFactor)
        {
            this.RedirectManager.RedirectTo(
                "account/login_with_2fa",
                new() { ["returnUrl"] = this.ReturnUrl, ["rememberMe"] = false });
            return;
        }

        // Unexpected — fall back to manual login.
        this.RedirectManager.RedirectTo(this.loginUrl);
    }

    private bool TryDecodeToken(out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(this.Code!));
            return true;
        }
        catch
        {
            decoded = string.Empty;
            return false;
        }
    }

    private void NavigateAfterInvite()
    {
        var target = this.ResolveReturnUrl();

        if (Uri.IsWellFormedUriString(target, UriKind.Absolute))
        {
            // Trusted absolute URL — full navigation, leaves the Blazor app (typical for SPA callback).
            this.NavigationManager.NavigateTo(target, forceLoad: true);
        }
        else
        {
            this.RedirectManager.RedirectTo(target);
        }
    }

    /// <summary>
    /// Resolves the post-invite target URL.
    /// Relative paths are always allowed. Absolute URLs are allowed only if their origin is in
    /// <see cref="CorsSettings.AllowedOrigins"/> — prevents open-redirect if an invite link leaks
    /// and an attacker manipulates <c>returnUrl</c>.
    /// </summary>
    private string ResolveReturnUrl()
    {
        const string fallback = "account/manage";

        if (string.IsNullOrWhiteSpace(this.ReturnUrl))
        {
            return fallback;
        }

        if (Uri.IsWellFormedUriString(this.ReturnUrl, UriKind.Relative))
        {
            return this.ReturnUrl;
        }

        if (!Uri.TryCreate(this.ReturnUrl, UriKind.Absolute, out var uri))
        {
            return fallback;
        }

        var origin = $"{uri.Scheme}://{uri.Authority}";
        var allowed = this.CorsSettings.Value.AllowedOrigins;
        if (allowed.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return this.ReturnUrl;
        }

        this.Logger.LogWarning(
            "Untrusted returnUrl {ReturnUrl} on accept_invite — falling back to {Fallback}.",
            this.ReturnUrl,
            fallback);

        return fallback;
    }

    private enum InviteState
    {
        Invalid,
        AlreadyActive,
        NeedsPassword,
    }

    private sealed class InputModel
    {
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
