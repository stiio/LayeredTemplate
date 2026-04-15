using LayeredTemplate.Auth.Web.Infrastructure.Options.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Auth.Web.Components.Account.Shared;

public partial class ReCaptcha : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string FormId { get; set; } = default!;

    [Inject]
    private IOptions<ReCaptchaSettings> ReCaptchaOptions { get; set; } = default!;

    private string SiteKey => this.ReCaptchaOptions.Value.SiteKey;

    private bool IsEnabled => !string.IsNullOrEmpty(this.SiteKey);

    private string SafeId => this.FormId.Replace("-", "_");
}
