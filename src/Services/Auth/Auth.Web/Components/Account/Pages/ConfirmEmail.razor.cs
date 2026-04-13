using System.Text;
using LayeredTemplate.Auth.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages;

public partial class ConfirmEmail : ComponentBase
{
    private string? statusMessage;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? UserId { get; set; }

    [SupplyParameterFromQuery]
    private string? Code { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (this.UserId is null || this.Code is null)
        {
            this.RedirectManager.RedirectTo(string.Empty);
            return;
        }

        var user = await this.UserManager.FindByIdAsync(this.UserId);
        if (user is null)
        {
            this.HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            this.statusMessage = $"Error loading user with ID {this.UserId}";
        }
        else
        {
            var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(this.Code));
            var result = await this.UserManager.ConfirmEmailAsync(user, code);
            this.statusMessage = result.Succeeded ? "Thank you for confirming your email." : "Error confirming your email.";
        }
    }
}
