using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class ResetPassword : ComponentBase
{
    private IEnumerable<IdentityError>? identityErrors;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private ILogger<ResetPassword> Logger { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    [SupplyParameterFromQuery]
    private string? Email { get; set; }

    private string? Message => this.identityErrors is null ? null : $"Error: {string.Join(", ", this.identityErrors.Select(error => error.Description))}";

    protected override void OnInitialized()
    {
        this.Input ??= new();

        if (this.Code is null)
        {
            this.RedirectManager.RedirectTo("account/invalid_password_reset");
            return;
        }

        if (string.IsNullOrEmpty(this.Email))
        {
            this.RedirectManager.RedirectTo("account/invalid_password_reset");
            return;
        }

        try
        {
            this.Input.Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(this.Code));
            this.Input.Email = WebUtility.UrlDecode(this.Email);
        }
        catch
        {
            this.RedirectManager.RedirectTo("account/invalid_password_reset");
        }

    }

    private async Task OnValidSubmitAsync()
    {
        var user = await this.UserManager.FindByEmailAsync(this.Input.Email);

        if (user is null)
        {
            // Don't reveal that the user does not exist
            this.RedirectManager.RedirectTo("account/reset_password_confirmation");
            return;
        }

        var result = await this.UserManager.ResetPasswordAsync(user, this.Input.Code, this.Input.Password);
        if (!result.Succeeded)
        {
            this.identityErrors = result.Errors;
            return;
        }

        this.Logger.LogInformation("Password reset for user {UserId}.", user.Id);

        // Clear any existing session (e.g. user was signed in as someone else).
        await this.SignInManager.SignOutAsync();

        // Auto-login. PasswordSignInAsync detects 2FA and sets the 2FA cookie if required.
        var signInResult = await this.SignInManager.PasswordSignInAsync(
            user,
            this.Input.Password,
            isPersistent: false,
            lockoutOnFailure: false);

        if (signInResult.Succeeded)
        {
            this.RedirectManager.RedirectTo("account/manage");
            return;
        }

        if (signInResult.RequiresTwoFactor)
        {
            this.RedirectManager.RedirectTo(
                "account/login_with_2fa",
                new() { ["rememberMe"] = false });
            return;
        }

        // IsNotAllowed (unconfirmed email) or IsLockedOut — can't auto-login, fall back to the
        // confirmation page and let the user sign in manually.
        this.RedirectManager.RedirectTo("account/reset_password_confirmation");
    }

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string Code { get; set; } = string.Empty;
    }
}
