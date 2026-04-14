using System.Text;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class ConfirmEmailChange : ComponentBase
{
    private string? message;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? UserId { get; set; }

    [SupplyParameterFromQuery]
    private string? Email { get; set; }

    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (this.UserId is null || this.Email is null || this.Code is null)
        {
            this.RedirectManager.RedirectToWithStatus(
                "Account/Login", "Error: Invalid email change confirmation link.", this.HttpContext);
            return;
        }

        var user = await this.UserManager.FindByIdAsync(this.UserId);
        if (user is null)
        {
            this.message = "Unable to find user with Id '{userId}'";
            return;
        }

        var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(this.Code));
        var result = await this.UserManager.ChangeEmailAsync(user, this.Email, code);
        if (!result.Succeeded)
        {
            this.message = "Error changing email.";
            return;
        }

        // In our UI email and user name are one and the same, so when we update the email
        // we need to update the user name.
        var setUserNameResult = await this.UserManager.SetUserNameAsync(user, this.Email);
        if (!setUserNameResult.Succeeded)
        {
            this.message = "Error changing user name.";
            return;
        }

        await this.SignInManager.RefreshSignInAsync(user);
        this.message = "Thank you for confirming your email change.";
    }
}
