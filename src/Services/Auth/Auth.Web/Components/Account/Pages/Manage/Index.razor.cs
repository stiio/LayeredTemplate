using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class Index : ComponentBase
{
    private ApplicationUser? user;
    private string? username;
    private string? phoneNumber;

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

        this.username = await this.UserManager.GetUserNameAsync(this.user);
        this.phoneNumber = await this.UserManager.GetPhoneNumberAsync(this.user);

        this.Input.PhoneNumber ??= this.phoneNumber;
    }

    private async Task OnValidSubmitAsync()
    {
        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        if (this.Input.PhoneNumber != this.phoneNumber)
        {
            var setPhoneResult = await this.UserManager.SetPhoneNumberAsync(this.user, this.Input.PhoneNumber);
            if (!setPhoneResult.Succeeded)
            {
                this.RedirectManager.RedirectToCurrentPageWithStatus("Error: Failed to set phone number.", this.HttpContext);
                return;
            }
        }

        await this.SignInManager.RefreshSignInAsync(this.user);
        this.RedirectManager.RedirectToCurrentPageWithStatus("Your profile has been updated", this.HttpContext);
    }

    private sealed class InputModel
    {
        [Phone]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }
    }
}
