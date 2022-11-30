using Microsoft.AspNetCore.Authentication;

namespace LayeredTemplate.Web.Mocks.Authentication;

public class MockAuthAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "MockAuth";

    public string Scheme => DefaultScheme;

    public string AuthenticationType => DefaultScheme;
}