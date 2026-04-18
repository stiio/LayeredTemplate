using LayeredTemplate.Auth.Web.Infrastructure.Data.Contexts;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.OpenIddict;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity;

public static class ServicesExtensions
{
    public static IdentityBuilder AddIdentityServices(this IServiceCollection services)
    {
        // Long-lived token provider for invitation links (separate from default reset tokens
        // so we can keep password-reset TTL short while invites stay valid for weeks).
        services.Configure<DataProtectionTokenProviderOptions>(
            InviteTokenSettings.ProviderName,
            o => o.TokenLifespan = InviteTokenSettings.Lifespan);

        return services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;

                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 6;
                options.Password.RequiredUniqueChars = 1;

                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>(InviteTokenSettings.ProviderName);
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