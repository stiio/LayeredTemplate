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

                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 6;
                options.Password.RequiredUniqueChars = 1;

                options.ClaimsIdentity.EmailClaimType = "email";
                options.ClaimsIdentity.UserIdClaimType = "userid";
                options.ClaimsIdentity.RoleClaimType = "role";
                options.ClaimsIdentity.UserNameClaimType = "name";

                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
    }
}