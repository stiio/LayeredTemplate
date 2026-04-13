using LayeredTemplate.Auth.Web.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class GenerateRecoveryCodes : ComponentBase
{
    private string? message;
    private ApplicationUser? user;
    private IEnumerable<string>? recoveryCodes;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private ILogger<GenerateRecoveryCodes> Logger { get; set; } = default!;

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

        var isTwoFactorEnabled = await this.UserManager.GetTwoFactorEnabledAsync(this.user);
        if (!isTwoFactorEnabled)
        {
            throw new InvalidOperationException("Cannot generate recovery codes for user because they do not have 2FA enabled.");
        }
    }

    private async Task OnSubmitAsync()
    {
        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        var userId = await this.UserManager.GetUserIdAsync(this.user);
        this.recoveryCodes = await this.UserManager.GenerateNewTwoFactorRecoveryCodesAsync(this.user, 10);
        this.message = "You have generated new recovery codes.";

        this.Logger.LogInformation("User with ID '{UserId}' has generated new 2FA recovery codes.", userId);
    }
}
