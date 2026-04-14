using System.ComponentModel.DataAnnotations;
using LayeredTemplate.Auth.Web.Data;
using LayeredTemplate.Auth.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class EditPhone : ComponentBase
{
    private string? message;
    private bool codeSent;
    private string? pendingPhone;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private ISmsSender SmsSender { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm(FormName = "send-code")]
    private PhoneInputModel PhoneInput { get; set; } = default!;

    [SupplyParameterFromForm(FormName = "verify-code")]
    private CodeInputModel CodeInput { get; set; } = default!;

    [SupplyParameterFromForm(Name = "ResendPhone")]
    private string? ResendPhone { get; set; }

    [SupplyParameterFromQuery]
    private string? Phone { get; set; }

    [SupplyParameterFromQuery]
    private bool CodeSent { get; set; }

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

        if (this.CodeSent && !string.IsNullOrEmpty(this.Phone))
        {
            this.codeSent = true;
            this.pendingPhone = this.Phone;
        }
        else
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

        if (!user.PhoneNumberConfirmed)
        {
            var code = await this.UserManager.GenerateChangePhoneNumberTokenAsync(user, phone);
            await this.SmsSender.SendAsync(phone, $"Your verification code is: {code}");
        }

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
            this.codeSent = true;
            this.pendingPhone = this.CodeInput.PhoneNumber;
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
        if (user is null || string.IsNullOrEmpty(this.ResendPhone))
        {
            return;
        }

        if (!user.PhoneNumberConfirmed)
        {
            var code = await this.UserManager.GenerateChangePhoneNumberTokenAsync(user, this.ResendPhone);
            await this.SmsSender.SendAsync(this.ResendPhone, $"Your verification code is: {code}");
        }

        this.RedirectManager.RedirectTo(
            "Account/Manage/EditPhone",
            new() { ["phone"] = this.ResendPhone, ["codeSent"] = true });
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
        public string? PhoneNumber { get; set; }

        [Required]
        [Display(Name = "Verification code")]
        public string? Code { get; set; }
    }
}
