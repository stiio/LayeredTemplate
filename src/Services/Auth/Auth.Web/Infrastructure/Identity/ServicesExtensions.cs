using LayeredTemplate.Auth.Web.Infrastructure.Data.Contexts;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Identity;

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
}