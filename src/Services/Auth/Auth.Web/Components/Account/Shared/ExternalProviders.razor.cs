using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;

namespace LayeredTemplate.Auth.Web.Components.Account.Shared;

public partial class ExternalProviders : ComponentBase
{
    private List<AuthenticationScheme>? providers;

    [Inject]
    private IAuthenticationSchemeProvider SchemeProvider { get; set; } = default!;

    [Parameter]
    public string? ReturnUrl { get; set; }

    [Parameter]
    public string ButtonText { get; set; } = "Sign in with";

    protected override async Task OnInitializedAsync()
    {
        var schemes = await this.SchemeProvider.GetAllSchemesAsync();
        this.providers = schemes
            .Where(s => !string.IsNullOrEmpty(s.DisplayName))
            .ToList();
    }
}
