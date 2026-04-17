using System.Security.Cryptography;
using Microsoft.AspNetCore.Components;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LayeredTemplate.Auth.Web.Components.Admin.Applications;

public partial class Create : ComponentBase
{
    private string? message;
    private string? generatedSecret;
    private List<string> availableScopes = [];

    [Inject]
    private IOpenIddictApplicationManager ApplicationManager { get; set; } = default!;

    [Inject]
    private IOpenIddictScopeManager ScopeManager { get; set; } = default!;

    [Inject]
    private ILogger<Create> Logger { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private ApplicationFormModel Input { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        // Must set Input before the first await — Blazor renders after the first yield,
        // and EditForm's Model must be non-null at that point.
        this.Input ??= new();
        this.availableScopes = await AdminScopeHelper.ListScopeNamesAsync(this.ScopeManager);
    }

    private async Task OnValidSubmitAsync()
    {
        var existing = await this.ApplicationManager.FindByClientIdAsync(this.Input.ClientId.Trim());
        if (existing is not null)
        {
            this.message = $"Error: an application with client ID '{this.Input.ClientId}' already exists.";
            return;
        }

        this.Input.Scopes = AdminScopeHelper.FilterToKnown(this.Input.Scopes, this.availableScopes);

        var descriptor = new OpenIddictApplicationDescriptor();
        this.Input.ApplyTo(descriptor);

        string? secret = null;
        if (descriptor.ClientType == ClientTypes.Confidential)
        {
            secret = GenerateClientSecret();
            descriptor.ClientSecret = secret;
        }

        await this.ApplicationManager.CreateAsync(descriptor);

        this.Logger.LogInformation("Admin created OpenIddict application {ClientId}.", this.Input.ClientId);

        this.generatedSecret = secret;
        if (secret is null)
        {
            // Public clients have no secret to show — redirect straight back.
            this.message = "Application created.";
        }
    }

    private static string GenerateClientSecret()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer);
    }
}
