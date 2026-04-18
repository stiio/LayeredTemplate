namespace LayeredTemplate.Auth.Web.Infrastructure.Identity;

public static class AppScopes
{
    public const string AdminUsers = "admin.users";

    /// <summary>
    /// Requests the user's roles to be surfaced in id_token. Access tokens for authenticated
    /// users always include <see cref="OpenIddict.Abstractions.OpenIddictConstants.Claims.Role"/>
    /// — this scope only gates id_token visibility.
    /// </summary>
    public const string Roles = "roles";
}

public static class AppAuthorizationPolicies
{
    public const string ScopeAdminUsers = "scope:admin.users";
}
