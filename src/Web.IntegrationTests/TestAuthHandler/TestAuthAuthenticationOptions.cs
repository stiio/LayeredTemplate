using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authentication;

namespace LayeredTemplate.Web.IntegrationTests.TestAuthHandler;

public class TestAuthAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "TestAuth";

    public string Scheme => DefaultScheme;

    public string AuthenticationType => AppAuthenticationTypes.Jwt;
}