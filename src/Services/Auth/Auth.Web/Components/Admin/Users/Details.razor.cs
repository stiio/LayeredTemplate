using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.Email.Services;
using LayeredTemplate.Auth.Web.Infrastructure.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LayeredTemplate.Auth.Web.Components.Admin.Users;

public partial class Details : ComponentBase
{
    private string? message;
    private bool notFound;
    private bool isSelf;
    private UserView? view;
    private List<string> availableRoles = [];

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private RoleManager<ApplicationRole> RoleManager { get; set; } = default!;

    [Inject]
    private IUserEmailSender EmailSender { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILogger<Details> Logger { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [Parameter]
    public string Id { get; set; } = string.Empty;

    [SupplyParameterFromForm(FormName = "edit-roles")]
    private RolesInputModel RolesInput { get; set; } = default!;

    [SupplyParameterFromForm(FormName = "set-password")]
    private SetPasswordInput PwdInput { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        this.PwdInput ??= new();
        this.availableRoles = await this.LoadAllRoleNamesAsync();
        this.isSelf = string.Equals(this.UserManager.GetUserId(this.HttpContext.User), this.Id, StringComparison.Ordinal);

        await this.LoadViewAsync();
        if (this.view is null)
        {
            return;
        }

        this.RolesInput ??= new RolesInputModel { Roles = this.view.Roles.ToList() };
    }

    private async Task LoadViewAsync()
    {
        var user = await this.UserManager.FindByIdAsync(this.Id);
        if (user is null)
        {
            this.notFound = true;
            this.view = null;
            return;
        }

        this.view = new UserView
        {
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneConfirmed = user.PhoneNumberConfirmed,
            HasPassword = await this.UserManager.HasPasswordAsync(user),
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = (await this.UserManager.GetRolesAsync(user)).ToList(),
        };
    }

    private async Task<List<string>> LoadAllRoleNamesAsync()
    {
        var names = new List<string>();
        foreach (var role in this.RoleManager.Roles)
        {
            if (!string.IsNullOrEmpty(role.Name))
            {
                names.Add(role.Name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return await Task.FromResult(names);
    }

    private async Task OnSaveRolesAsync()
    {
        var user = await this.UserManager.FindByIdAsync(this.Id);
        if (user is null)
        {
            this.notFound = true;
            return;
        }

        // Filter against known roles — protects against crafted POST values.
        var requested = this.RolesInput.Roles.Intersect(this.availableRoles, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var current = (await this.UserManager.GetRolesAsync(user)).ToHashSet(StringComparer.Ordinal);

        // Guard: admin cannot strip the Admin role from their own account — would lock themselves out.
        if (this.isSelf && current.Contains(AppRoles.Admin) && !requested.Contains(AppRoles.Admin))
        {
            this.message = "Error: you cannot remove the Admin role from your own account. Use another admin to do that.";
            await this.LoadViewAsync();
            return;
        }

        var toAdd = requested.Except(current).ToList();
        var toRemove = current.Except(requested).ToList();

        if (toAdd.Count > 0)
        {
            var addResult = await this.UserManager.AddToRolesAsync(user, toAdd);
            if (!addResult.Succeeded)
            {
                this.message = $"Error: {string.Join(" ", addResult.Errors.Select(e => e.Description))}";
                await this.LoadViewAsync();
                return;
            }
        }

        if (toRemove.Count > 0)
        {
            var removeResult = await this.UserManager.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
            {
                this.message = $"Error: {string.Join(" ", removeResult.Errors.Select(e => e.Description))}";
                await this.LoadViewAsync();
                return;
            }
        }

        if (toAdd.Count > 0 || toRemove.Count > 0)
        {
            this.Logger.LogWarning(
                "Admin updated roles for user {UserId}. Added: [{Added}], removed: [{Removed}].",
                this.Id,
                string.Join(", ", toAdd),
                string.Join(", ", toRemove));
            this.message = "Roles saved.";
        }
        else
        {
            this.message = "No role changes.";
        }

        await this.LoadViewAsync();
    }

    private async Task OnSendConfirmationAsync()
    {
        var user = await this.UserManager.FindByIdAsync(this.Id);
        if (user is null || string.IsNullOrEmpty(user.Email))
        {
            this.notFound = user is null;
            this.message = user is null ? null : "Error: user has no email.";
            return;
        }

        if (user.EmailConfirmed)
        {
            this.message = "Email is already confirmed.";
            return;
        }

        var userId = await this.UserManager.GetUserIdAsync(user);
        var code = await this.UserManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = this.NavigationManager.GetUriWithQueryParameters(
            this.NavigationManager.ToAbsoluteUri("account/confirm_email").AbsoluteUri,
            new Dictionary<string, object?> { ["userId"] = userId, ["code"] = code });

        await this.EmailSender.SendConfirmationLinkAsync(user, user.Email, HtmlEncoder.Default.Encode(callbackUrl));

        this.Logger.LogInformation("Admin sent email confirmation link for user {UserId}.", this.Id);
        this.message = "Confirmation email sent.";
    }

    private async Task OnSendPasswordResetAsync()
    {
        var user = await this.UserManager.FindByIdAsync(this.Id);
        if (user is null || string.IsNullOrEmpty(user.Email))
        {
            this.notFound = user is null;
            this.message = user is null ? null : "Error: user has no email.";
            return;
        }

        var code = await this.UserManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = this.NavigationManager.GetUriWithQueryParameters(
            this.NavigationManager.ToAbsoluteUri("account/reset_password").AbsoluteUri,
            new Dictionary<string, object?> { ["code"] = code, ["email"] = WebUtility.UrlEncode(user.Email) });

        await this.EmailSender.SendPasswordResetLinkAsync(user, user.Email, HtmlEncoder.Default.Encode(callbackUrl));

        this.Logger.LogInformation("Admin sent password reset link for user {UserId}.", this.Id);
        this.message = "Password reset email sent.";
    }

    private async Task OnSetPasswordAsync()
    {
        var user = await this.UserManager.FindByIdAsync(this.Id);
        if (user is null)
        {
            this.notFound = true;
            return;
        }

        IdentityResult result;
        if (await this.UserManager.HasPasswordAsync(user))
        {
            var token = await this.UserManager.GeneratePasswordResetTokenAsync(user);
            result = await this.UserManager.ResetPasswordAsync(user, token, this.PwdInput.NewPassword);
        }
        else
        {
            result = await this.UserManager.AddPasswordAsync(user, this.PwdInput.NewPassword);
        }

        if (!result.Succeeded)
        {
            this.message = $"Error: {string.Join(" ", result.Errors.Select(e => e.Description))}";
            await this.LoadViewAsync();
            return;
        }

        this.Logger.LogWarning("Admin set password manually for user {UserId}.", this.Id);
        this.message = "Password updated.";
        this.PwdInput = new();
        await this.LoadViewAsync();
    }

    private sealed class UserView
    {
        public string Email { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public string? PhoneNumber { get; set; }
        public bool PhoneConfirmed { get; set; }
        public bool HasPassword { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public List<string> Roles { get; set; } = [];
    }

    private sealed class RolesInputModel
    {
        public List<string> Roles { get; set; } = [];
    }

    private sealed class SetPasswordInput
    {
        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
