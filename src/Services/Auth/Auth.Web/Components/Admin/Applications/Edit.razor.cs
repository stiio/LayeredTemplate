using System.Security.Cryptography;
using Microsoft.AspNetCore.Components;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LayeredTemplate.Auth.Web.Components.Admin.Applications;

public partial class Edit : ComponentBase
{
    private string? message;
    private string? regeneratedSecret;
    private bool notFound;
    private bool isConfidential;

    [Inject]
    private IOpenIddictApplicationManager ApplicationManager { get; set; } = default!;

    [Inject]
    private ILogger<Edit> Logger { get; set; } = default!;

    [Parameter]
    public string ClientId { get; set; } = string.Empty;

    [SupplyParameterFromForm(FormName = "edit-application")]
    private ApplicationFormModel? Input { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var app = await this.ApplicationManager.FindByClientIdAsync(this.ClientId);
        if (app is null)
        {
            this.notFound = true;
            return;
        }

        this.isConfidential = string.Equals(
            await this.ApplicationManager.GetClientTypeAsync(app),
            ClientTypes.Confidential,
            StringComparison.OrdinalIgnoreCase);

        if (this.Input is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await this.ApplicationManager.PopulateAsync(descriptor, app);
            this.Input = ApplicationFormModel.FromDescriptor(descriptor);
        }
    }

    private async Task OnValidSubmitAsync()
    {
        if (this.Input is null)
        {
            return;
        }

        var app = await this.ApplicationManager.FindByClientIdAsync(this.ClientId);
        if (app is null)
        {
            this.notFound = true;
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await this.ApplicationManager.PopulateAsync(descriptor, app);

        // ClientId edit is disabled in UI; keep form value aligned with route.
        this.Input.ClientId = this.ClientId;
        this.Input.ApplyTo(descriptor);

        // Preserve existing client secret on plain updates — secret is rotated via a separate action.
        descriptor.ClientSecret = null;

        await this.ApplicationManager.PopulateAsync(app, descriptor);
        await this.ApplicationManager.UpdateAsync(app);

        this.Logger.LogInformation("Admin updated OpenIddict application {ClientId}.", this.ClientId);
        this.message = "Application saved.";

        this.isConfidential = descriptor.ClientType == ClientTypes.Confidential;
    }

    private async Task OnRegenerateSecretAsync()
    {
        var app = await this.ApplicationManager.FindByClientIdAsync(this.ClientId);
        if (app is null)
        {
            this.notFound = true;
            return;
        }

        if (!string.Equals(
                await this.ApplicationManager.GetClientTypeAsync(app),
                ClientTypes.Confidential,
                StringComparison.OrdinalIgnoreCase))
        {
            this.message = "Error: only confidential clients have a secret.";
            return;
        }

        var newSecret = GenerateClientSecret();
        await this.ApplicationManager.UpdateAsync(app, newSecret);

        this.Logger.LogWarning("Admin regenerated client secret for {ClientId}.", this.ClientId);
        this.regeneratedSecret = newSecret;
        this.message = "New client secret generated.";
    }

    private static string GenerateClientSecret()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer);
    }
}
