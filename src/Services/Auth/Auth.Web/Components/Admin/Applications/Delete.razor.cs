using LayeredTemplate.Auth.Web.Components.Account;
using Microsoft.AspNetCore.Components;
using OpenIddict.Abstractions;

namespace LayeredTemplate.Auth.Web.Components.Admin.Applications;

public partial class Delete : ComponentBase
{
    private bool notFound;

    [Inject]
    private IOpenIddictApplicationManager ApplicationManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private ILogger<Delete> Logger { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [Parameter]
    public string ClientId { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var app = await this.ApplicationManager.FindByClientIdAsync(this.ClientId);
        if (app is null)
        {
            this.notFound = true;
        }
    }

    private async Task OnDeleteAsync()
    {
        var app = await this.ApplicationManager.FindByClientIdAsync(this.ClientId);
        if (app is null)
        {
            this.notFound = true;
            return;
        }

        await this.ApplicationManager.DeleteAsync(app);
        this.Logger.LogWarning("Admin deleted OpenIddict application {ClientId}.", this.ClientId);

        this.RedirectManager.RedirectToWithStatus(
            "admin/applications",
            $"Application '{this.ClientId}' deleted.",
            this.HttpContext);
    }
}
