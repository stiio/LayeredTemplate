using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class ResetAuthenticator : ComponentBase
{
    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private ILogger<ResetAuthenticator> Logger { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    private async Task OnSubmitAsync()
    {
        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        await this.UserManager.SetTwoFactorEnabledAsync(user, false);
        await this.UserManager.ResetAuthenticatorKeyAsync(user);
        var userId = await this.UserManager.GetUserIdAsync(user);
        this.Logger.LogInformation("User with ID '{UserId}' has reset their authentication app key.", userId);

        await this.SignInManager.RefreshSignInAsync(user);

        this.RedirectManager.RedirectToWithStatus(
            "Account/Manage/EnableAuthenticator",
            "Your authenticator app key has been reset, you will need to configure your authenticator app using the new key.",
            this.HttpContext);
    }
}
