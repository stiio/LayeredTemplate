using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Infrastructure.AuthorizationHandlers;
using LayeredTemplate.Infrastructure.Services;
using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure.Extensions;

internal static class AuthorizationExtensions
{
    public static void ConfigureAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IClaimsTransformation, AppClaimTransformation>();

        services.AddScoped<IResourceAuthorizationService, ResourceAuthorizationService>();
        services.AddScoped<IAuthorizationHandler, TodoListAuthorizationHandler>();

        services.AddAuthorization(ConfigurePolicies);
    }

    private static void ConfigurePolicies(AuthorizationOptions options)
    {
        options.AddPolicy(Policies.Example, Policies.ExamplePolicy);
    }
}