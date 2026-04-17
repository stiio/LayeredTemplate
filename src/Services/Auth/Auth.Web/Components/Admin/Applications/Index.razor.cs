using Microsoft.AspNetCore.Components;
using OpenIddict.Abstractions;

namespace LayeredTemplate.Auth.Web.Components.Admin.Applications;

public partial class Index : ComponentBase
{
    private const int MaxApplications = 200;

    private List<ApplicationRow>? applications;

    [Inject]
    private IOpenIddictApplicationManager ApplicationManager { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        var rows = new List<ApplicationRow>();
        await foreach (var app in this.ApplicationManager.ListAsync(count: MaxApplications, offset: 0))
        {
            rows.Add(new ApplicationRow(
                ClientId: await this.ApplicationManager.GetClientIdAsync(app) ?? string.Empty,
                DisplayName: await this.ApplicationManager.GetDisplayNameAsync(app),
                ClientType: await this.ApplicationManager.GetClientTypeAsync(app)));
        }

        this.applications = rows.OrderBy(r => r.ClientId, StringComparer.Ordinal).ToList();
    }

    private sealed record ApplicationRow(string ClientId, string? DisplayName, string? ClientType);
}
