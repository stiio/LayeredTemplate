using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class ConnectedAccounts : ComponentBase
{
    private string? message;
    private List<ProviderState>? providerStates;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private IAuthenticationSchemeProvider SchemeProvider { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm(Name = "LoginProvider")]
    private string? RemoveLoginProvider { get; set; }

    [SupplyParameterFromForm(Name = "ProviderKey")]
    private string? RemoveProviderKey { get; set; }

    [SupplyParameterFromQuery(Name = "handler")]
    private string? Handler { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        // Handle link callback from external provider
        if (this.Handler == "link_callback")
        {
            await this.HandleLinkCallbackAsync(user);
        }

        var currentLogins = await this.UserManager.GetLoginsAsync(user);
        var allSchemes = await this.SchemeProvider.GetAllSchemesAsync();
        var externalSchemes = allSchemes
            .Where(s => !string.IsNullOrEmpty(s.DisplayName))
            .ToList();

        this.providerStates = externalSchemes.Select(scheme =>
        {
            var login = currentLogins.FirstOrDefault(l => l.LoginProvider == scheme.Name);
            return new ProviderState
            {
                Name = scheme.Name,
                DisplayName = scheme.DisplayName ?? scheme.Name,
                IsConnected = login is not null,
                ProviderKey = login?.ProviderKey,
            };
        }).ToList();
    }

    private async Task HandleLinkCallbackAsync(ApplicationUser user)
    {
        var info = await this.SignInManager.GetExternalLoginInfoAsync(await this.UserManager.GetUserIdAsync(user));
        if (info is null)
        {
            this.message = "Error: Could not load external login info.";
            return;
        }

        var result = await this.UserManager.AddLoginAsync(user, info);
        if (!result.Succeeded)
        {
            this.message = $"Error: {string.Join(", ", result.Errors.Select(e => e.Description))}";
            return;
        }

        // Clear the external cookie
        await this.HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        this.message = $"{info.LoginProvider} has been connected.";
    }

    private async Task OnRemoveLoginAsync()
    {
        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null || this.RemoveLoginProvider is null || this.RemoveProviderKey is null)
        {
            return;
        }

        var result = await this.UserManager.RemoveLoginAsync(user, this.RemoveLoginProvider, this.RemoveProviderKey);
        if (!result.Succeeded)
        {
            this.message = "Error: Failed to disconnect the provider.";
            return;
        }

        await this.SignInManager.RefreshSignInAsync(user);
        this.RedirectManager.RedirectToCurrentPageWithStatus($"{this.RemoveLoginProvider} has been disconnected.", this.HttpContext);
    }

    private sealed class ProviderState
    {
        public string Name { get; set; } = default!;

        public string DisplayName { get; set; } = default!;

        public bool IsConnected { get; set; }

        public string? ProviderKey { get; set; }
    }
}
