using LayeredTemplate.Auth.Web.Infrastructure.Identity.Contexts;

namespace LayeredTemplate.Auth.Web.Extensions;

public static class OpenIddictExtensions
{
    public static OpenIddictBuilder AddOpenIddictApp(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        return services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<AuthDbContext>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .SetUserInfoEndpointUris("/connect/userinfo")
                    .SetEndSessionEndpointUris("/connect/logout");

                options.AllowAuthorizationCodeFlow()
                    .RequireProofKeyForCodeExchange();

                options.AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();

                options.RegisterScopes("openid", "profile", "email");

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
            });
    }
}