using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class EditProfile : ComponentBase
{
    private string? message;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        // Must set Input before the first await — Blazor renders after the first yield
        // and EditForm.Model must be non-null at that point.
        this.Input ??= new();

        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        // Pre-fill on first GET. On post-back SupplyParameterFromForm has already populated
        // Input with submitted values, so ??= leaves them alone.
        this.Input.FirstName ??= user.FirstName;
        this.Input.LastName ??= user.LastName;
    }

    private async Task OnValidSubmitAsync()
    {
        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        user.FirstName = string.IsNullOrWhiteSpace(this.Input.FirstName) ? null : this.Input.FirstName.Trim();
        user.LastName = string.IsNullOrWhiteSpace(this.Input.LastName) ? null : this.Input.LastName.Trim();

        var result = await this.UserManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            this.message = $"Error: {string.Join(" ", result.Errors.Select(e => e.Description))}";
            return;
        }

        this.RedirectManager.RedirectToWithStatus(
            "account/manage",
            "Your profile has been updated.",
            this.HttpContext);
    }

    private sealed class InputModel
    {
        [MaxLength(100)]
        [Display(Name = "First name")]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        [Display(Name = "Last name")]
        public string? LastName { get; set; }
    }
}
