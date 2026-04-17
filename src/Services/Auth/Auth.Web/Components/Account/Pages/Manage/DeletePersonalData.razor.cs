using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.Options.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class DeletePersonalData : ComponentBase
{
    private string? message;
    private ApplicationUser? user;
    private bool requirePassword;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private ILogger<DeletePersonalData> Logger { get; set; } = default!;

    [Inject]
    private IOptions<AppSettings> AppSettings { get; set; } = default!;

    private bool IsDeletePersonalDataEnabled => this.AppSettings.Value.IsDeletePersonalDataEnabled;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (!this.IsDeletePersonalDataEnabled)
        {
            this.RedirectManager.RedirectTo("not_found");
            return;
        }

        this.Input ??= new();

        this.user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        this.requirePassword = await this.UserManager.HasPasswordAsync(this.user);
    }

    private async Task OnValidSubmitAsync()
    {
        if (!this.IsDeletePersonalDataEnabled)
        {
            this.RedirectManager.RedirectTo("not_found");
            return;
        }

        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        if (this.requirePassword && !await this.UserManager.CheckPasswordAsync(this.user, this.Input.Password))
        {
            this.message = "Error: Incorrect password.";
            return;
        }

        var result = await this.UserManager.DeleteAsync(this.user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Unexpected error occurred deleting user.");
        }

        await this.SignInManager.SignOutAsync();

        var userId = await this.UserManager.GetUserIdAsync(this.user);
        this.Logger.LogInformation("User with ID '{UserId}' deleted themselves.", userId);

        this.RedirectManager.RedirectToCurrentPage();
    }

    private sealed class InputModel
    {
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
