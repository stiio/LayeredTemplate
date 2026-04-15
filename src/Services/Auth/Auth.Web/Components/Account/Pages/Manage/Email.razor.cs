using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class Email : ComponentBase
{
    private string? message;
    private ApplicationUser? user;
    private string? email;
    private bool isEmailConfirmed;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IEmailSender<ApplicationUser> EmailSender { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm(FormName = "change-email")]
    private InputModel Input { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        this.Input ??= new();

        this.user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        this.email = await this.UserManager.GetEmailAsync(this.user);
        this.isEmailConfirmed = await this.UserManager.IsEmailConfirmedAsync(this.user);

        this.Input.NewEmail ??= this.email;
    }

    private async Task OnValidSubmitAsync()
    {
        if (this.Input.NewEmail is null || this.Input.NewEmail == this.email)
        {
            this.message = "Your email is unchanged.";
            return;
        }

        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        var userId = await this.UserManager.GetUserIdAsync(this.user);
        var code = await this.UserManager.GenerateChangeEmailTokenAsync(this.user, this.Input.NewEmail);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = this.NavigationManager.GetUriWithQueryParameters(
            this.NavigationManager.ToAbsoluteUri("account/confirm_email_change").AbsoluteUri,
            new Dictionary<string, object?> { ["userId"] = userId, ["email"] = this.Input.NewEmail, ["code"] = code });

        await this.EmailSender.SendConfirmationLinkAsync(this.user, this.Input.NewEmail, HtmlEncoder.Default.Encode(callbackUrl));

        this.message = "Confirmation link to change email sent. Please check your email.";
    }

    private async Task OnSendEmailVerificationAsync()
    {
        if (this.email is null)
        {
            return;
        }

        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        var userId = await this.UserManager.GetUserIdAsync(this.user);
        var code = await this.UserManager.GenerateEmailConfirmationTokenAsync(this.user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = this.NavigationManager.GetUriWithQueryParameters(
            this.NavigationManager.ToAbsoluteUri("account/confirm_email").AbsoluteUri,
            new Dictionary<string, object?> { ["userId"] = userId, ["code"] = code });

        await this.EmailSender.SendConfirmationLinkAsync(this.user, this.email, HtmlEncoder.Default.Encode(callbackUrl));

        this.message = "Verification email sent. Please check your email.";
    }

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [MaxLength(128)]
        [Display(Name = "New email")]
        public string? NewEmail { get; set; }
    }
}
