using LayeredTemplate.Auth.Web.Infrastructure.Data.Contexts;
using LayeredTemplate.Auth.Web.Infrastructure.StartupTasks;
using LayeredTemplate.Plugins.StartupRunner;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace LayeredTemplate.Auth.Web.Infrastructure.OpenIddict;

public static class ServicesExtensions
{
    public static OpenIddictBuilder AddAppOpenIddict(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        services.AddOpenIddictKetRotation();
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
                    .AllowRefreshTokenFlow()
                    .AllowClientCredentialsFlow()
                    .RequireProofKeyForCodeExchange();

                options.DisableAccessTokenEncryption();

                options.SetAccessTokenLifetime(TimeSpan.FromHours(1))
                    .SetIdentityTokenLifetime(TimeSpan.FromHours(1))
                    .SetRefreshTokenLifetime(TimeSpan.FromDays(30));

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                // Auth.Web validates its own tokens (same signing/encryption keys).
                options.UseLocalServer();
                options.UseAspNetCore();
                options.AddAudiences(AppResources.ApiAuthWeb);
            });
    }

    private static void AddOpenIddictKetRotation(this IServiceCollection services)
    {
        // Singleton store populated by RotateSigningKeysTask, consumed by PostConfigure
        services.AddSingleton<SigningKeyStore>();
        services.AddSingleton<IPostConfigureOptions<OpenIddictServerOptions>, ConfigureOpenIddictServerOptions>();
        services.AddStartupTask<RotateSigningKeysTask>();
    }
}
