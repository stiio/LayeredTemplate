using LayeredTemplate.Auth.Web.Infrastructure.Data.Contexts;
using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity;

public static class ServicesExtensions
{
    public static IdentityBuilder AddIdentityServices(this IServiceCollection services)
    {
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
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
    }
}