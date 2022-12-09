using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Web.Api.Extensions;

/// <summary>
/// Authorization Extensions
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Configure Authorization
    /// </summary>
    /// <param name="services"></param>
    public static void ConfigureAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(ConfigurePolicies);
    }

    private static void ConfigurePolicies(AuthorizationOptions options)
    {
        options.AddPolicy(Policies.Example, Policies.ExamplePolicy);
    }
}