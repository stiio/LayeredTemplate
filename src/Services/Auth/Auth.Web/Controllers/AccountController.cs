using System.Text.Json;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Auth.Web.Controllers;

[Route("account")]
public class AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) : Controller
{
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        await signInManager.SignOutAsync();
        return this.LocalRedirect(returnUrl ?? "~/account/login");
    }

    [HttpPost("external_login")]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = $"/account/external_login_callback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/account/manage")}";
        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return this.Challenge(properties, provider);
    }

    [Authorize]
    [HttpPost("manage/download_personal_data")]
    public async Task<IActionResult> DownloadPersonalData()
    {
        var user = await userManager.GetUserAsync(this.User);
        if (user is null)
        {
            return this.NotFound($"Unable to load user with ID '{userManager.GetUserId(this.User)}'.");
        }

        var personalData = new Dictionary<string, string>();

        var personalDataProps = typeof(ApplicationUser)
            .GetProperties()
            .Where(prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));

        foreach (var prop in personalDataProps)
        {
            personalData.Add(prop.Name, prop.GetValue(user)?.ToString() ?? "null");
        }

        var logins = await userManager.GetLoginsAsync(user);
        foreach (var login in logins)
        {
            personalData.Add($"{login.LoginProvider} external login provider key", login.ProviderKey);
        }

        personalData.Add("Authenticator Key", (await userManager.GetAuthenticatorKeyAsync(user))!);

        return this.File(
            JsonSerializer.SerializeToUtf8Bytes(personalData),
            "application/json",
            "PersonalData.json");
    }
}
