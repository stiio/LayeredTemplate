using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account.Pages.Manage;

public partial class EnableAuthenticator : ComponentBase
{
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    private string? message;
    private ApplicationUser? user;
    private string? sharedKey;
    private string? authenticatorUri;
    private string? qrCodeDataUri;
    private IEnumerable<string>? recoveryCodes;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private UrlEncoder UrlEncoder { get; set; } = default!;

    [Inject]
    private IdentityRedirectManager RedirectManager { get; set; } = default!;

    [Inject]
    private ILogger<EnableAuthenticator> Logger { get; set; } = default!;

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

        await this.LoadSharedKeyAndQrCodeUriAsync();
    }

    private static string GenerateQrCodeDataUri(string text)
    {
        using var qrGenerator = new QRCoder.QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(text, QRCoder.QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
        var pngBytes = qrCode.GetGraphic(5);
        return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
    }

    private async Task OnValidSubmitAsync()
    {
        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        // Strip spaces and hyphens
        var verificationCode = this.Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        var is2FaTokenValid = await this.UserManager.VerifyTwoFactorTokenAsync(
            this.user, this.UserManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

        if (!is2FaTokenValid)
        {
            this.message = "Error: Verification code is invalid.";
            return;
        }

        await this.UserManager.SetTwoFactorEnabledAsync(this.user, true);
        var userId = await this.UserManager.GetUserIdAsync(this.user);
        this.Logger.LogInformation("User with ID '{UserId}' has enabled 2FA with an authenticator app.", userId);

        this.message = "Your authenticator app has been verified.";

        if (await this.UserManager.CountRecoveryCodesAsync(this.user) == 0)
        {
            this.recoveryCodes = await this.UserManager.GenerateNewTwoFactorRecoveryCodesAsync(this.user, 10);
        }
        else
        {
            this.RedirectManager.RedirectToWithStatus("account/manage/two_factor_authentication", this.message, this.HttpContext);
        }
    }

    private async ValueTask LoadSharedKeyAndQrCodeUriAsync()
    {
        if (this.user is null)
        {
            this.RedirectManager.RedirectToInvalidUser(this.UserManager, this.HttpContext);
            return;
        }

        // Load the authenticator key & QR code URI to display on the form
        var unformattedKey = await this.UserManager.GetAuthenticatorKeyAsync(this.user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await this.UserManager.ResetAuthenticatorKeyAsync(this.user);
            unformattedKey = await this.UserManager.GetAuthenticatorKeyAsync(this.user);
        }

        this.sharedKey = this.FormatKey(unformattedKey!);

        var email = await this.UserManager.GetEmailAsync(this.user);
        this.authenticatorUri = this.GenerateQrCodeUri(email!, unformattedKey!);
        this.qrCodeDataUri = GenerateQrCodeDataUri(this.authenticatorUri);
    }

    private string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        int currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    private string GenerateQrCodeUri(string email, string unformattedKey)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            AuthenticatorUriFormat,
            this.UrlEncoder.Encode("Microsoft.AspNetCore.Identity.UI"),
            this.UrlEncoder.Encode(email),
            unformattedKey);
    }

    private sealed class InputModel
    {
        [Required]
        [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Verification Code")]
        public string Code { get; set; } = string.Empty;
    }
}
