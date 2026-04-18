using LayeredTemplate.Auth.Web.Infrastructure.Identity;
using LayeredTemplate.Auth.Web.Infrastructure.OpenIddict;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace LayeredTemplate.Auth.Web.Infrastructure.Authorization;

public static class ServicesExtensions
{
    public static void AddAppAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddGitHub(opts =>
            {
                opts.ClientId = configuration["GitHub:ClientId"]!;
                opts.ClientSecret = configuration["GitHub:ClientSecret"]!;
                opts.CallbackPath = "/signin-github";
                opts.Scope.Add("user:email");
            })
            .AddYandex(opts =>
            {
                opts.ClientId = configuration["Yandex:ClientId"]!;
                opts.ClientSecret = configuration["Yandex:ClientSecret"]!;
                opts.CallbackPath = "/signin-yandex";
            })
            .AddIdentityCookies();
    }

    public static void AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
        {
            opts.AddPolicy(AppRoles.Admin, policy => policy.RequireRole(AppRoles.Admin));

            opts.AddPolicy(AppAuthorizationPolicies.ScopeAdminUsers, policy =>
            {
                policy.AuthenticationSchemes.Clear();
                policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx => ctx.User.HasScope(AppScopes.AdminUsers));
            });
        });
    }
}