using LayeredTemplate.Infrastructure.Mocks.Authentication;
using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure.Extensions;

internal static class AuthenticationExtensions
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
            // TODO: Configure authentication. Change USE_MOCK_AUTH to false
        }
    }
}