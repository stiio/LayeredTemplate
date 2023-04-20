using LayeredTemplate.Infrastructure.Mocks.Authentication;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Shared.Options;
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
            services.AddAuthentication(AppAuthenticationSchemes.User)
                .AddScheme<MockAuthAuthenticationOptions, MockAuthHandler>(MockAuthAuthenticationOptions.DefaultScheme, _ => { });
        }
        else
        {
            var cognitoSettings = configuration.GetSection(nameof(CognitoSettings)).Get<CognitoSettings>()!;
            services.AddAuthentication(AppAuthenticationSchemes.User)
                .AddJwtBearer(options =>
                {
                    options.Authority = $"https://cognito-idp.{configuration["AWS_REGION"]}.amazonaws.com/{cognitoSettings.UserPoolId}";
                    options.Audience = cognitoSettings.Audience;
                    options.TokenValidationParameters.AuthenticationType = AppAuthenticationTypes.User;
                });
        }
    }
}