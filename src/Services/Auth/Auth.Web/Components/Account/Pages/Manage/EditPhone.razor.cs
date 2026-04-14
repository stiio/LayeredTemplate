using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.Sms;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

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
        await this.SendVerificationCodeAsync(user, phone);

        this.RedirectManager.RedirectTo(
            "Account/Manage/EditPhone",
            new() { ["phone"] = phone, ["codeSent"] = true });
    }

    private async Task OnVerifyCodeAsync()
    {
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
            "Account/Manage",
            "Your phone number has been updated.",
            this.HttpContext);
    }

    private async Task OnResendCodeAsync()
    {
        var user = await this.UserManager.GetUserAsync(this.HttpContext.User);
        if (user is null || string.IsNullOrEmpty(this.Phone))
        {
            return;
        }

        await this.SendVerificationCodeAsync(user, this.Phone);

        this.RedirectManager.RedirectTo(
            "Account/Manage/EditPhone",
            new() { ["phone"] = this.Phone, ["codeSent"] = true });
    }

    private async Task SendVerificationCodeAsync(ApplicationUser user, string phone)
    {
        if (!user.PhoneNumberConfirmed)
        {
            var code = await this.UserManager.GenerateChangePhoneNumberTokenAsync(user, phone);
            await this.SmsSender.SendAsync(phone, $"Your verification code is: {code}");
        }
    }

    private sealed class PhoneInputModel
    {
        [Required]
        [Phone]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }
    }

    private sealed class CodeInputModel
    {
        /// <summary>Passed via hidden input to preserve phone across form submit.</summary>
        public string? PhoneNumber { get; set; }

        [Required]
        [Display(Name = "Verification code")]
        public string? Code { get; set; }
    }
}
