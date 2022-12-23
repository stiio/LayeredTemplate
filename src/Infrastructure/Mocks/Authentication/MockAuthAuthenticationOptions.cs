using Microsoft.AspNetCore.Authentication;

namespace LayeredTemplate.Infrastructure.Mocks.Authentication;

internal class MockAuthAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "MockAuth";

    public string Scheme => DefaultScheme;

    public string AuthenticationType => DefaultScheme;
}