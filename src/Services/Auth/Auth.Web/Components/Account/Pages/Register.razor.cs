using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.ReCaptcha;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class Register : ComponentBase
{
    private IEnumerable<IdentityError>? identityErrors;
    private string? captchaError;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IUserStore<ApplicationUser> UserStore { get; set; } = default!;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private IEmailSender<ApplicationUser> EmailSender { get; set; } = default!;

    [Inject]
    private ILogger<Register> Logger { get; set; } = default!;

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

    private string? Message => this.captchaError
                               ?? (this.identityErrors is null ? null : $"Error: {string.Join(", ", this.identityErrors.Select(error => error.Description))}");

    protected override void OnInitialized()
    {
        this.Input ??= new();
    }

    private async Task RegisterUser()
    {
        if (!await this.ReCaptchaService.ValidateAsync(this.HttpContext))
        {
            this.captchaError = "Error: reCAPTCHA validation failed. Please try again.";
            return;
        }

        var user = this.CreateUser();

        await this.UserStore.SetUserNameAsync(user, this.Input.Email, CancellationToken.None);
        var emailStore = this.GetEmailStore();
        await emailStore.SetEmailAsync(user, this.Input.Email, CancellationToken.None);
        var result = await this.UserManager.CreateAsync(user, this.Input.Password);

        if (!result.Succeeded)
        {
            this.identityErrors = result.Errors;
            return;
        }

        this.Logger.LogInformation("User created a new account with password.");

        var userId = await this.UserManager.GetUserIdAsync(user);
        var code = await this.UserManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = this.NavigationManager.GetUriWithQueryParameters(
            this.NavigationManager.ToAbsoluteUri("account/confirm_email").AbsoluteUri,
            new Dictionary<string, object?> { ["userId"] = userId, ["code"] = code, ["returnUrl"] = this.ReturnUrl });

        await this.EmailSender.SendConfirmationLinkAsync(user, this.Input.Email, HtmlEncoder.Default.Encode(callbackUrl));

        if (this.UserManager.Options.SignIn.RequireConfirmedAccount)
        {
            this.RedirectManager.RedirectTo(
                "account/register_confirmation",
                new() { ["email"] = this.Input.Email, ["returnUrl"] = this.ReturnUrl });
        }
        else
        {
            await this.SignInManager.SignInAsync(user, isPersistent: false);
            this.RedirectManager.RedirectTo(this.ReturnUrl);
        }
    }

    private ApplicationUser CreateUser()
    {
        try
        {
            return Activator.CreateInstance<ApplicationUser>();
        }
        catch
        {
            throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                                                $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor.");
        }
    }

    private IUserEmailStore<ApplicationUser> GetEmailStore()
    {
        if (!this.UserManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }

        return (IUserEmailStore<ApplicationUser>)this.UserStore;
    }

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
