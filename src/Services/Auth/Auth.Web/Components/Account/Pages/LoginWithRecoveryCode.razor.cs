using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Infrastructure.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class LoginWithRecoveryCode : ComponentBase
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
    private ILogger<LoginWithRecoveryCode> Logger { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
        this.Input ??= new();

        // Ensure the user has gone through the username & password screen first
        this.user = await this.SignInManager.GetTwoFactorAuthenticationUserAsync() ??
               throw new InvalidOperationException("Unable to load two-factor authentication user.");
    }

    private async Task OnValidSubmitAsync()
    {
        var recoveryCode = this.Input.RecoveryCode.Replace(" ", string.Empty);

        var result = await this.SignInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);

        var userId = await this.UserManager.GetUserIdAsync(this.user);

        if (result.Succeeded)
        {
            this.Logger.LogInformation("User with ID '{UserId}' logged in with a recovery code.", userId);
            this.RedirectManager.RedirectTo(this.ReturnUrl);
        }
        else if (result.IsLockedOut)
        {
            this.Logger.LogWarning("User account locked out.");
            this.RedirectManager.RedirectTo("Account/Lockout");
        }
        else
        {
            this.Logger.LogWarning("Invalid recovery code entered for user with ID '{UserId}' ", userId);
            this.message = "Error: Invalid recovery code entered.";
        }
    }

    private sealed class InputModel
    {
        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Recovery Code")]
        public string RecoveryCode { get; set; } = string.Empty;
    }
}
