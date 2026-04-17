namespace LayeredTemplate.Auth.Web.Infrastructure.Identity;

public static class AppScopes
{
    public const string AdminUsers = "admin.users";
}

public static class AppAuthorizationPolicies
{
    public const string ScopeAdminUsers = "scope:admin.users";
}
