using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.Web.Extensions;

public static class AuthorizationExtensions
{
    public static void ConfigureAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(ConfigurePolicies);
    }

    private static void ConfigurePolicies(AuthorizationOptions options)
    {
        options.AddPolicy(Policies.Example, Policies.ExamplePolicy);
    }
}