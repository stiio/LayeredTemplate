using System.Text.Json;
using LayeredTemplate.Auth.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.Auth.Web.Controllers;

[Route("[controller]")]
public class AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) : Controller
{
    [HttpPost("Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        await signInManager.SignOutAsync();
        return LocalRedirect(returnUrl ?? "/");
    }

    [Authorize]
    [HttpPost("Manage/DownloadPersonalData")]
    public async Task<IActionResult> DownloadPersonalData()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
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

        return File(
            JsonSerializer.SerializeToUtf8Bytes(personalData),
            "application/json",
            "PersonalData.json");
    }
}
