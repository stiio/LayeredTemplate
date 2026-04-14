using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class ForgotPassword : ComponentBase
{
    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IEmailSender<ApplicationUser> EmailSender { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    protected override void OnInitialized()
    {
        this.Input ??= new();
    }

    private async Task OnValidSubmitAsync()
    {
        var user = await this.UserManager.FindByEmailAsync(this.Input.Email);
        if (user is null || !(await this.UserManager.IsEmailConfirmedAsync(user)))
        {
            // Don't reveal that the user does not exist or is not confirmed
            this.RedirectManager.RedirectTo("account/forgot_password_confirmation");
            return;
        }

        // For more information on how to enable account confirmation and password reset please
        // visit https://go.microsoft.com/fwlink/?LinkID=532713
        var code = await this.UserManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = this.NavigationManager.GetUriWithQueryParameters(
            this.NavigationManager.ToAbsoluteUri("account/reset_password").AbsoluteUri,
            new Dictionary<string, object?> { ["code"] = code });

        await this.EmailSender.SendPasswordResetLinkAsync(user, this.Input.Email, HtmlEncoder.Default.Encode(callbackUrl));

        this.RedirectManager.RedirectTo("account/forgot_password_confirmation");
    }

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
