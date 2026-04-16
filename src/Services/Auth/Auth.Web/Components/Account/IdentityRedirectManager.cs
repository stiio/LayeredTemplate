using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Components.Account;

internal sealed class IdentityRedirectManager(NavigationManager navigationManager)
{
    public const string StatusCookieName = "Identity.StatusMessage";

    private static readonly CookieBuilder StatusCookieBuilder = new()
    {
        SameSite = SameSiteMode.Strict,
        HttpOnly = true,
        IsEssential = true,
        MaxAge = TimeSpan.FromSeconds(5),
    };

    private string CurrentPath => navigationManager.ToAbsoluteUri(navigationManager.Uri).GetLeftPart(UriPartial.Path);

    public void RedirectTo(string? uri)
    {
        uri ??= string.Empty;

        // Prevent open redirects.
        if (!Uri.IsWellFormedUriString(uri, UriKind.Relative))
        {
            uri = navigationManager.ToBaseRelativePath(uri);
        }

        navigationManager.NavigateTo(uri);
    }

    public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
    {
        var uriWithoutQuery = navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
        var newUri = navigationManager.GetUriWithQueryParameters(uriWithoutQuery, queryParameters);
        this.RedirectTo(newUri);
    }

    public void RedirectToWithStatus(string uri, string message, HttpContext context)
    {
        context.Response.Cookies.Append(StatusCookieName, message, StatusCookieBuilder.Build(context));
        this.RedirectTo(uri);
    }

    public void RedirectToCurrentPage() => this.RedirectTo(this.CurrentPath);

    public void RedirectToCurrentPageWithStatus(string message, HttpContext context)
        => this.RedirectToWithStatus(this.CurrentPath, message, context);

    public void RedirectToInvalidUser(UserManager<ApplicationUser> userManager, HttpContext context)
        => this.RedirectToWithStatus(
            "account/invalid_user",
            $"Error: Unable to load user with ID '{userManager.GetUserId(context.User)}'.",
            context);
}