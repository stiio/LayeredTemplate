using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.Shared.Constants;

public static class Policies
{
    public const string Example = "ExamplePolicy";

    public static AuthorizationPolicy ExamplePolicy => new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(AppClaims.Role, Roles.Admin)
        .Build();
}