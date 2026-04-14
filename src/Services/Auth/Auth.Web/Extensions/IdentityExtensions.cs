using LayeredTemplate.Auth.Web.Infrastructure.Identity.Contexts;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Extensions;

public static class IdentityExtensions
{
    public static IdentityBuilder AddIdentityServices(this IServiceCollection services)
    {
        return services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
    }
}