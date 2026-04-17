using LayeredTemplate.Auth.Web.Components.Account;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Admin.Users;

public partial class Delete : ComponentBase
{
    private string? message;
    private bool notFound;
    private bool isSelf;
    private string? email;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private ILogger<Delete> Logger { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [Parameter]
    public string Id { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var user = await this.UserManager.FindByIdAsync(this.Id);
        if (user is null)
        {
            this.notFound = true;
            return;
        }

        this.email = user.Email;
        this.isSelf = string.Equals(this.UserManager.GetUserId(this.HttpContext.User), this.Id, StringComparison.Ordinal);
    }

    private async Task OnDeleteAsync()
    {
        if (this.isSelf)
        {
            this.message = "Error: cannot delete your own account.";
            return;
        }

        var user = await this.UserManager.FindByIdAsync(this.Id);
        if (user is null)
        {
            this.notFound = true;
            return;
        }

        var result = await this.UserManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            this.message = $"Error: {string.Join(" ", result.Errors.Select(e => e.Description))}";
            return;
        }

        this.Logger.LogWarning(
            "Admin {AdminId} deleted user {UserId} ({Email}).",
            this.UserManager.GetUserId(this.HttpContext.User),
            this.Id,
            user.Email);

        this.RedirectManager.RedirectToWithStatus(
            "admin/users",
            $"User '{user.Email}' deleted.",
            this.HttpContext);
    }
}
