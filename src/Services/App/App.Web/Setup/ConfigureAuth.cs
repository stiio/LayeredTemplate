using System.Reflection;
using LayeredTemplate.App.Shared.Auth;
using LayeredTemplate.App.Shared.Auth.MockAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace LayeredTemplate.App.Setup;

public static class ConfigureAuth
{
    /// <summary>
    /// Registers authentication + authorization. Two modes:
    /// <list type="bullet">
    /// <item><c>USE_MOCK_AUTH=true</c> — Mock handler reads <see cref="MockUserSettings"/> from config (Dev/Test only).</item>
    /// <item>otherwise — JwtBearer validating tokens issued by <c>Authentication:Authority</c> (production).</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddAppAuth(this IServiceCollection services, IConfiguration configuration)
    {
        // Mock auth settings always bound — even if not used, integration tests rely on it.
        services.Configure<MockUserSettings>(configuration.GetSection(nameof(MockUserSettings)));

        var useMockAuth = configuration.GetValue<bool>("USE_MOCK_AUTH");
        if (useMockAuth)
        {
            services.AddAuthentication(AppAuthenticationSchemes.Bearer)
                .AddScheme<AuthenticationSchemeOptions, MockAuthHandler>(AppAuthenticationSchemes.Bearer, _ => { });
        }
        else
        {
            services.AddAuthentication(AppAuthenticationSchemes.Bearer)
                .AddJwtBearer(AppAuthenticationSchemes.Bearer, options =>
                {
                    options.Authority = configuration["Authentication:Authority"];
                    options.Audience = "api://app-web";
                    options.TokenValidationParameters.ValidateAudience = true;
                    options.TokenValidationParameters.ValidTypes = ["at+jwt"];
                });
        }

        services.AddTransient<IAuthorizationPolicyProvider, HasPermissionPolicyProvider>();
        services.AddAuthorization(opts =>
        {
            opts.InvokeHandlersAfterFailure = false;
        });

        // Auto-register custom IAuthorizationHandler implementations in the assembly.
        var handlers = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IAuthorizationHandler).IsAssignableFrom(t) && t is { IsGenericType: false, IsAbstract: false, IsInterface: false });
        foreach (var handler in handlers)
        {
            services.AddScoped(typeof(IAuthorizationHandler), handler);
        }

        return services;
    }
}
