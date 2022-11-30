using Microsoft.AspNetCore.Authentication;

namespace LayeredTemplate.Web.Api.IntegrationTests.TestAuth;

public class TestAuthAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "TestAuth";

    public string Scheme => DefaultScheme;

    public string AuthenticationType => DefaultScheme;
}