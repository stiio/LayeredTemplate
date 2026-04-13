using Microsoft.AspNetCore.Components;

namespace LayeredTemplate.Auth.Web.Components.Account.Shared;

public partial class StatusMessage : ComponentBase
{
    private string? messageFromCookie;

    [Parameter]
    public string? Message { get; set; }

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    private string? DisplayMessage => this.Message ?? this.messageFromCookie;

    protected override void OnInitialized()
    {
        this.messageFromCookie = this.HttpContext.Request.Cookies[IdentityRedirectManager.StatusCookieName];

        if (this.messageFromCookie is not null)
        {
            this.HttpContext.Response.Cookies.Delete(IdentityRedirectManager.StatusCookieName);
        }
    }
}
