using System.Security.Claims;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class ExternalLoginCallback : ComponentBase
{
    private string? message;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IUserStore<ApplicationUser> UserStore { get; set; } = default!;

    [Inject]
    private ILogger<ExternalLoginCallback> Logger { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var info = await this.SignInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            this.message = "Error: Could not load external login information.";
            return;
        }

        // Try to sign in with the external login
        var result = await this.SignInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

        if (result.Succeeded)
        {
            this.Logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity?.Name, info.LoginProvider);
            this.RedirectManager.RedirectTo(this.ReturnUrl ?? "account/manage");
            return;
        }

        if (result.IsLockedOut)
        {
            this.RedirectManager.RedirectTo("account/lockout");
            return;
        }

        // User doesn't have an account — create one
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            this.message = "Error: Email claim not received from external provider.";
            return;
        }

        var user = new ApplicationUser();
        await this.UserStore.SetUserNameAsync(user, email, CancellationToken.None);

        var emailStore = (IUserEmailStore<ApplicationUser>)this.UserStore;
        await emailStore.SetEmailAsync(user, email, CancellationToken.None);

        var createResult = await this.UserManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            this.message = $"Error: {string.Join(", ", createResult.Errors.Select(e => e.Description))}";
            return;
        }

        // Confirm email automatically for external logins
        var token = await this.UserManager.GenerateEmailConfirmationTokenAsync(user);
        await this.UserManager.ConfirmEmailAsync(user, token);

        var addLoginResult = await this.UserManager.AddLoginAsync(user, info);
        if (!addLoginResult.Succeeded)
        {
            this.message = $"Error: {string.Join(", ", addLoginResult.Errors.Select(e => e.Description))}";
            return;
        }

        await this.SignInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
        this.Logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

        this.RedirectManager.RedirectTo(this.ReturnUrl ?? "account/manage");
    }
}
