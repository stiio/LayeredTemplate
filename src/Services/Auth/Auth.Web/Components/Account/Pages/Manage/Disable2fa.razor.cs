using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class Disable2fa : ComponentBase
{
    private ApplicationUser? user;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private ILogger<Disable2fa> Logger { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        this.user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        if (HttpMethods.IsGet(this.HttpContext.Request.Method) && !await this.UserManager.GetTwoFactorEnabledAsync(this.user))
        {
            throw new InvalidOperationException("Cannot disable 2FA for user as it's not currently enabled.");
        }
    }

    private async Task OnSubmitAsync()
    {
        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        var disable2FaResult = await this.UserManager.SetTwoFactorEnabledAsync(this.user, false);
        if (!disable2FaResult.Succeeded)
        {
            throw new InvalidOperationException("Unexpected error occurred disabling 2FA.");
        }

        var userId = await this.UserManager.GetUserIdAsync(this.user);
        this.Logger.LogInformation("User with ID '{UserId}' has disabled 2fa.", userId);
        this.RedirectManager.RedirectToWithStatus(
            "account/manage/two_factor_authentication",
            "2fa has been disabled. You can reenable 2fa when you setup an authenticator app",
            this.HttpContext);
    }
}
