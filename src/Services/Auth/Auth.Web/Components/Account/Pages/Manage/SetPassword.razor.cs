using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class SetPassword : ComponentBase
{
    private string? message;
    private ApplicationUser? user;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        this.Input ??= new();

        this.user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        var hasPassword = await this.UserManager.HasPasswordAsync(this.user);
        if (hasPassword)
        {
            this.RedirectManager.RedirectTo("account/manage/change_password");
        }
    }

    private async Task OnValidSubmitAsync()
    {
        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        var addPasswordResult = await this.UserManager.AddPasswordAsync(this.user, this.Input.NewPassword!);
        if (!addPasswordResult.Succeeded)
        {
            this.message = $"Error: {string.Join(",", addPasswordResult.Errors.Select(error => error.Description))}";
            return;
        }

        await this.SignInManager.RefreshSignInAsync(this.user);
        this.RedirectManager.RedirectToCurrentPageWithStatus("Your password has been set.", this.HttpContext);
    }

    private sealed class InputModel
    {
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }
    }
}
