using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
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
        catch (Exception e)
        {
            this.RedirectManager.RedirectTo("account/invalid_password_reset");
            return;
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
        if (result.Succeeded)
        {
            this.RedirectManager.RedirectTo("account/reset_password_confirmation");
            return;
        }

        this.identityErrors = result.Errors;
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
