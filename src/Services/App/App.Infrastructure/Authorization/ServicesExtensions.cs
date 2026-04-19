using System.Reflection;
using LayeredTemplate.App.Infrastructure.Authorization.Mocks;
using LayeredTemplate.App.Infrastructure.Authorization.PolicyProviders;
using LayeredTemplate.App.Shared.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.App.Infrastructure.Authorization;

public static class ServicesExtensions
{
    public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Mock auth settings for development
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
                .AddJwtBearer(AppAuthenticationSchemes.Bearer,
                    options =>
                    {
                        options.Authority = configuration["Authentication:Authority"];
                        options.Audience = "api://app-web";
                        options.TokenValidationParameters.ValidateAudience = true;
                        options.TokenValidationParameters.ValidTypes = ["at+jwt"];
                    });
        }
    }

    public static void ConfigureAuthorization(this IServiceCollection services)
    {
        services.AddTransient<IAuthorizationPolicyProvider, CustomAuthorizationPolicyProvider>();

        services.AddAuthorization(opts =>
        {
            opts.InvokeHandlersAfterFailure = false;
        });

        var authorizationHandlers = Assembly.GetExecutingAssembly().GetTypes().Where(type =>
            type.IsAssignableTo(typeof(IAuthorizationHandler)) && type is { IsGenericType: false, IsAbstract: false }).ToArray();
        foreach (var authorizationHandler in authorizationHandlers)
        {
            services.AddScoped(typeof(IAuthorizationHandler), authorizationHandler);
        }
    }
}