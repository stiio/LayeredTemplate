using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.Shared.Constants;

public static class Policies
{
    public const string Admin = "Admin";

    public const string Client = "Client";

    public static AuthorizationPolicy AdminPolicy => new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(TokenKeys.Role, Roles.Admin)
        .Build();

    public static AuthorizationPolicy ClientPolicy => new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(TokenKeys.Role, Roles.Client)
        .Build();
}