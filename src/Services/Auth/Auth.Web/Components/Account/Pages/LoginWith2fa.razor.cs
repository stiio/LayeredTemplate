using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class LoginWith2fa : ComponentBase
{
    private string? message;

    private ApplicationUser user = default!;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private ILogger<LoginWith2fa> Logger { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    [SupplyParameterFromQuery]
    private bool RememberMe { get; set; }

    protected override async Task OnInitializedAsync()
    {
        this.Input ??= new();

        // Ensure the user has gone through the username & password screen first
        this.user = await this.SignInManager.GetTwoFactorAuthenticationUserAsync() ??
               throw new InvalidOperationException("Unable to load two-factor authentication user.");
    }

    private async Task OnValidSubmitAsync()
    {
        var authenticatorCode = this.Input.TwoFactorCode!.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await this.SignInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, this.RememberMe, this.Input.RememberMachine);
        var userId = await this.UserManager.GetUserIdAsync(this.user);

        if (result.Succeeded)
        {
            this.Logger.LogInformation("User with ID '{UserId}' logged in with 2fa.", userId);
            this.RedirectManager.RedirectTo(this.ReturnUrl);
        }
        else if (result.IsLockedOut)
        {
            this.Logger.LogWarning("User with ID '{UserId}' account locked out.", userId);
            this.RedirectManager.RedirectTo("account/lockout");
        }
        else
        {
            this.Logger.LogWarning("Invalid authenticator code entered for user with ID '{UserId}'.", userId);
            this.message = "Error: Invalid authenticator code.";
        }
    }

    private sealed class InputModel
    {
        [Required]
        [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Authenticator code")]
        public string? TwoFactorCode { get; set; }

        [Display(Name = "Remember this machine")]
        public bool RememberMachine { get; set; }
    }
}
