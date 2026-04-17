using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.Options.Models;
using LayeredTemplate.Auth.Web.Infrastructure.Sms.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class EditPhone : ComponentBase
{
    private string? message;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private ISmsSender SmsSender { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private IOptions<AppSettings> AppSettings { get; set; } = default!;

    private bool IsPhoneConfirmationEnabled => this.AppSettings.Value.IsPhoneConfirmationEnabled;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    /// <summary>Bound to the "send-code" form (step 1: enter phone number).</summary>
    [SupplyParameterFromForm(FormName = "send-code")]
    private PhoneInputModel PhoneInput { get; set; } = default!;

    /// <summary>Bound to the "verify-code" form (step 2: enter verification code + hidden phone).</summary>
    [SupplyParameterFromForm(FormName = "verify-code")]
    private CodeInputModel CodeInput { get; set; } = default!;

    /// <summary>Phone number passed via query string after code is sent. Drives step 2 UI.</summary>
    [SupplyParameterFromQuery]
    private string? Phone { get; set; }

    /// <summary>Flag from query string indicating we're on step 2 (code entry).</summary>
    [SupplyParameterFromQuery]
    private bool CodeSent { get; set; }

    /// <summary>True when we're on step 2 (code was sent, waiting for verification).</summary>
    private bool IsVerifyStep => this.CodeSent && !string.IsNullOrEmpty(this.Phone);

    protected override async Task OnInitializedAsync()
    {
        this.PhoneInput ??= new();
        this.CodeInput ??= new();

        // When confirmation is disabled, the verify step is unreachable.
        if (!this.IsPhoneConfirmationEnabled && this.IsVerifyStep)
        {
            this.RedirectManager.RedirectTo("not_found");
            return;
        }

        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        if (!this.IsVerifyStep)
        {
            var currentPhone = await this.UserManager.GetPhoneNumberAsync(user);
            this.PhoneInput.PhoneNumber ??= currentPhone;
        }
    }

    private async Task OnSendCodeAsync()
    {
        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        var phone = this.PhoneInput.PhoneNumber!;

        if (!this.IsPhoneConfirmationEnabled)
        {
            // SetPhoneNumberAsync updates the number and resets PhoneNumberConfirmed to false.
            var setResult = await this.UserManager.SetPhoneNumberAsync(user, phone);
            if (!setResult.Succeeded)
            {
                this.message = $"Error: {string.Join(" ", setResult.Errors.Select(e => e.Description))}";
                return;
            }

            this.RedirectManager.RedirectToWithStatus(
                "account/manage",
                "Your phone number has been updated.",
                this.HttpContext);
            return;
        }

        await this.SendVerificationCodeAsync(user, phone);

        this.RedirectManager.RedirectTo(
            "account/manage/edit_phone",
            new() { ["phone"] = phone, ["codeSent"] = true });
    }

    private async Task OnVerifyCodeAsync()
    {
        if (!this.IsPhoneConfirmationEnabled)
        {
            this.RedirectManager.RedirectTo("not_found");
            return;
        }

        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        var result = await this.UserManager.ChangePhoneNumberAsync(
            user,
            this.CodeInput.PhoneNumber!,
            this.CodeInput.Code!);

        if (!result.Succeeded)
        {
            this.message = "Error: Invalid verification code.";
            return;
        }

        this.RedirectManager.RedirectToWithStatus(
            "account/manage",
            "Your phone number has been updated.",
            this.HttpContext);
    }

    private async Task OnResendCodeAsync()
    {
        if (!this.IsPhoneConfirmationEnabled)
        {
            this.RedirectManager.RedirectTo("not_found");
            return;
        }

        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null || string.IsNullOrEmpty(this.Phone))
        {
            return;
        }

        await this.SendVerificationCodeAsync(user, this.Phone);

        this.RedirectManager.RedirectTo(
            "account/manage/edit_phone",
            new() { ["phone"] = this.Phone, ["codeSent"] = true });
    }

    private async Task SendVerificationCodeAsync(ApplicationUser user, string phone)
    {
        var code = await this.UserManager.GenerateChangePhoneNumberTokenAsync(user, phone);
        await this.SmsSender.SendAsync(phone, $"Your verification code is: {code}");
    }

    private sealed class PhoneInputModel
    {
        [Required]
        [Phone]
        [MaxLength(20)]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }
    }

    private sealed class CodeInputModel
    {
        /// <summary>Passed via hidden input to preserve phone across form submit.</summary>
        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        [Display(Name = "Verification code")]
        public string? Code { get; set; }
    }
}
