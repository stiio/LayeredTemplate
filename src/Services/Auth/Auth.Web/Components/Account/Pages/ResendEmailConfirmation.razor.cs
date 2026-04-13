using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using LayeredTemplate.Auth.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class ResendEmailConfirmation : ComponentBase
{
    private string? message;

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
        var user = await this.UserManager.FindByEmailAsync(this.Input.Email!);
        if (user is null)
        {
            this.message = "Verification email sent. Please check your email.";
            return;
        }

        var userId = await this.UserManager.GetUserIdAsync(user);
        var code = await this.UserManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = this.NavigationManager.GetUriWithQueryParameters(
            this.NavigationManager.ToAbsoluteUri("Account/ConfirmEmail").AbsoluteUri,
            new Dictionary<string, object?> { ["userId"] = userId, ["code"] = code });
        await this.EmailSender.SendConfirmationLinkAsync(user, this.Input.Email, HtmlEncoder.Default.Encode(callbackUrl));

        this.message = "Verification email sent. Please check your email.";
    }

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
