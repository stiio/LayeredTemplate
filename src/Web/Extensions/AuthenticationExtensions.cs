using LayeredTemplate.Shared.Options;
using LayeredTemplate.Web.Mocks.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace LayeredTemplate.Web.Extensions;

public static class AuthenticationExtensions
{
    public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var cognitoSettings = configuration.GetSection(nameof(CognitoSettings)).Get<CognitoSettings>()!;
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://cognito-idp.{configuration["AWS_REGION"]}.amazonaws.com/{cognitoSettings.UserPoolId}";
                options.Audience = cognitoSettings.Audience;
            });

        var useMockAuth = configuration.GetValue<bool>("USE_MOCK_AUTH");
        if (useMockAuth)
        {
            services.AddAuthentication(MockAuthAuthenticationOptions.DefaultScheme)
                .AddScheme<MockAuthAuthenticationOptions, MockAuthHandler>(MockAuthAuthenticationOptions.DefaultScheme, _ => { });
        }
    }
}