using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class TwoFactorAuthentication : ComponentBase
{
    private bool canTrack;
    private bool hasAuthenticator;
    private int recoveryCodesLeft;
    private bool is2FaEnabled;
    private bool isMachineRemembered;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

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

        this.canTrack = this.HttpContext.Features.Get<ITrackingConsentFeature>()?.CanTrack ?? true;
        this.hasAuthenticator = await this.UserManager.GetAuthenticatorKeyAsync(user) is not null;
        this.is2FaEnabled = await this.UserManager.GetTwoFactorEnabledAsync(user);
        this.isMachineRemembered = await this.SignInManager.IsTwoFactorClientRememberedAsync(user);
        this.recoveryCodesLeft = await this.UserManager.CountRecoveryCodesAsync(user);
    }

    private async Task OnSubmitForgetBrowserAsync()
    {
        await this.SignInManager.ForgetTwoFactorClientAsync();

        this.RedirectManager.RedirectToCurrentPageWithStatus(
            "The current browser has been forgotten. When you login again from this browser you will be prompted for your 2fa code.",
            this.HttpContext);
    }
}
