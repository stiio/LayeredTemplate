using System.Text;
using System.Text.Encodings.Web;
using LayeredTemplate.Auth.Web.Data;
using LayeredTemplate.Auth.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class Index : ComponentBase
{
    private string? username;
    private string? email;
    private string? phoneNumber;
    private bool isEmailConfirmed;
    private bool isPhoneConfirmed;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IEmailSender<ApplicationUser> EmailSender { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ISmsSender SmsSender { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        this.username = await this.UserManager.GetUserNameAsync(user);
        this.email = await this.UserManager.GetEmailAsync(user);
        this.phoneNumber = await this.UserManager.GetPhoneNumberAsync(user);
        this.isEmailConfirmed = await this.UserManager.IsEmailConfirmedAsync(user);
        this.isPhoneConfirmed = user.PhoneNumberConfirmed;
    }

    private async Task OnSendEmailVerificationAsync()
    {
        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null || this.email is null)
        {
            return;
        }

        var userId = await this.UserManager.GetUserIdAsync(user);
        var code = await this.UserManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = this.NavigationManager.GetUriWithQueryParameters(
            this.NavigationManager.ToAbsoluteUri("Account/ConfirmEmail").AbsoluteUri,
            new Dictionary<string, object?> { ["userId"] = userId, ["code"] = code });

        await this.EmailSender.SendConfirmationLinkAsync(user, this.email, HtmlEncoder.Default.Encode(callbackUrl));

        this.RedirectManager.RedirectToCurrentPageWithStatus("Verification email sent. Please check your email.", this.HttpContext);
    }

    private async Task OnSendPhoneVerificationAsync()
    {
        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null || string.IsNullOrEmpty(this.phoneNumber))
        {
            return;
        }

        if (!user.PhoneNumberConfirmed)
        {
            var code = await this.UserManager.GenerateChangePhoneNumberTokenAsync(user, this.phoneNumber);
            await this.SmsSender.SendAsync(this.phoneNumber, $"Your verification code is: {code}");
        }

        this.RedirectManager.RedirectTo(
            "Account/Manage/EditPhone",
            new() { ["phone"] = this.phoneNumber, ["codeSent"] = true });
    }
}
