using System.Text;
using LayeredTemplate.Auth.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class RegisterConfirmation : ComponentBase
{
    private string? emailConfirmationLink;
    private string? statusMessage;

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

    [SupplyParameterFromQuery]
    private string? Email { get; set; }

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (this.Email is null)
        {
            this.RedirectManager.RedirectTo(string.Empty);
            return;
        }

        var user = await this.UserManager.FindByEmailAsync(this.Email);
        if (user is null)
        {
            this.HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            this.statusMessage = "Error finding user for unspecified email";
        }
        else if (this.EmailSender is IdentityNoOpEmailSender)
        {
            // Once you add a real email sender, you should remove this code that lets you confirm the account
            var userId = await this.UserManager.GetUserIdAsync(user);
            var code = await this.UserManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            this.emailConfirmationLink = this.NavigationManager.GetUriWithQueryParameters(
                this.NavigationManager.ToAbsoluteUri("Account/ConfirmEmail").AbsoluteUri,
                new Dictionary<string, object?> { ["userId"] = userId, ["code"] = code, ["returnUrl"] = this.ReturnUrl });
        }
    }
}
