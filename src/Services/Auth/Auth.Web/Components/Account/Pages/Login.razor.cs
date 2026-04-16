using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.ReCaptcha;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class Login : ComponentBase
{
    private string? errorMessage;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private ILogger<Login> Logger { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private ReCaptchaService ReCaptchaService { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    public async Task LoginUser()
    {
        if (!await this.ReCaptchaService.ValidateAsync(this.HttpContext))
        {
            this.errorMessage = "Error: reCAPTCHA validation failed. Please try again.";
            return;
        }

        // This doesn't count login failures towards account lockout
        // To enable password failures to trigger account lockout, set lockoutOnFailure: true
        var result = await this.SignInManager.PasswordSignInAsync(this.Input.Email, this.Input.Password, this.Input.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            this.Logger.LogInformation("User logged in.");
            this.RedirectManager.RedirectTo(this.ReturnUrl ?? "account/manage");
        }
        else if (result.RequiresTwoFactor)
        {
            this.RedirectManager.RedirectTo(
                "account/login_with_2fa",
                new() { ["returnUrl"] = this.ReturnUrl, ["rememberMe"] = this.Input.RememberMe });
        }
        else if (result.IsLockedOut)
        {
            this.Logger.LogWarning("User account locked out.");
            this.RedirectManager.RedirectTo("account/lockout");
        }
        else
        {
            this.errorMessage = "Error: Invalid login attempt.";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        this.Input ??= new();

        if (HttpMethods.IsGet(this.HttpContext.Request.Method))
        {
            // Clear the existing external cookie to ensure a clean login process
            await this.HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }
    }

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
}
