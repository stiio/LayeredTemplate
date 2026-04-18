namespace LayeredTemplate.Auth.Web.Infrastructure.OpenIddict;

public static class AppScopes
{
    /// <summary>
    /// Requests the user's roles to be surfaced in id_token and access_token.
    /// </summary>
    public const string Roles = "roles";

    public const string AdminUsers = "auth/admin.users";
}