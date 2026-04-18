using System.Text.Json;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Auth.Web.Controllers;

[Route("account")]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> signInManager;
    private readonly UserManager<ApplicationUser> userManager;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        this.signInManager = signInManager;
        this.userManager = userManager;
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        await this.signInManager.SignOutAsync();
        return this.LocalRedirect(returnUrl ?? "~/account/login");
    }

    [HttpPost("external_login")]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = $"/account/external_login_callback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/account/manage")}";
        var properties = this.signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return this.Challenge(properties, provider);
    }

    [Authorize]
    [HttpPost("manage/link_external_login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkExternalLogin(string provider)
    {
        await this.HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var redirectUrl = "/account/manage/connected_accounts?handler=link_callback";
        var properties = this.signInManager.ConfigureExternalAuthenticationProperties(
            provider,
            redirectUrl,
            this.userManager.GetUserId(this.User));
        return this.Challenge(properties, provider);
    }

    [Authorize]
    [HttpPost("manage/download_personal_data")]
    public async Task<IActionResult> DownloadPersonalData()
    {
        var user = await this.userManager.GetUserAsync(this.User);
        if (user is null)
        {
            return this.NotFound($"Unable to load user with ID '{this.userManager.GetUserId(this.User)}'.");
        }

        var personalData = new Dictionary<string, string>();

        var personalDataProps = typeof(ApplicationUser)
            .GetProperties()
            .Where(prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));

        foreach (var prop in personalDataProps)
        {
            personalData.Add(prop.Name, prop.GetValue(user)?.ToString() ?? "null");
        }

        var logins = await this.userManager.GetLoginsAsync(user);
        foreach (var login in logins)
        {
            personalData.Add($"{login.LoginProvider} external login provider key", login.ProviderKey);
        }

        personalData.Add("Authenticator Key", (await this.userManager.GetAuthenticatorKeyAsync(user))!);

        return this.File(
            JsonSerializer.SerializeToUtf8Bytes(personalData),
            "application/json",
            "PersonalData.json");
    }
}
