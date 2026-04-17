using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.Email.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LayeredTemplate.Auth.Web.Components.Admin.Users;

public partial class Edit : ComponentBase
{
    private string? message;
    private bool notFound;
    private UserView? view;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IUserEmailSender EmailSender { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILogger<Edit> Logger { get; set; } = default!;

    [Parameter]
    public string Id { get; set; } = string.Empty;

    [SupplyParameterFromForm(FormName = "set-password")]
    private SetPasswordInput PwdInput { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        this.PwdInput ??= new();
        await this.LoadViewAsync();
    }

    private async Task<ApplicationUser?> LoadViewAsync()
    {
        var user = await this.UserManager.FindByIdAsync(this.Id);
        if (user is null)
        {
            this.notFound = true;
            this.view = null;
            return null;
        }

        this.view = new UserView
        {
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneConfirmed = user.PhoneNumberConfirmed,
            HasPassword = await this.UserManager.HasPasswordAsync(user),
            Roles = (await this.UserManager.GetRolesAsync(user)).ToList(),
        };

        return user;
    }

    private async Task OnConfirmEmailAsync()
    {
        var user = await this.UserManager.FindByIdAsync(this.Id);
        if (user is null)
        {
            this.notFound = true;
            return;
        }

        if (user.EmailConfirmed)
        {
            this.message = "Email is already confirmed.";
            await this.LoadViewAsync();
            return;
        }

        user.EmailConfirmed = true;
        var result = await this.UserManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            this.message = $"Error: {string.Join(" ", result.Errors.Select(e => e.Description))}";
            await this.LoadViewAsync();
            return;
        }

        this.Logger.LogWarning("Admin manually confirmed email for user {UserId}.", this.Id);
        this.message = "Email marked as confirmed.";
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
