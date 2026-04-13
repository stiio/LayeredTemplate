using Microsoft.AspNetCore.Components;

namespace LayeredTemplate.Auth.Web.Components.Account.Shared;

public partial class RedirectToLogin : ComponentBase
{
    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    protected override void OnInitialized()
    {
        this.NavigationManager.NavigateTo(
            $"Account/Login?returnUrl={Uri.EscapeDataString(this.NavigationManager.Uri)}",
            forceLoad: true);
    }
}
