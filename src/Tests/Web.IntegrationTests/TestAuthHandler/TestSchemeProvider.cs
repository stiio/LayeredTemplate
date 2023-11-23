using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Web.IntegrationTests.TestAuthHandler;

public class TestSchemeProvider : AuthenticationSchemeProvider
{
    public TestSchemeProvider(IOptions<AuthenticationOptions> options)
        : base(options)
    {
    }

    protected TestSchemeProvider(IOptions<AuthenticationOptions> options, IDictionary<string, AuthenticationScheme> schemes)
        : base(options, schemes)
    {
    }

    public override Task<AuthenticationScheme?> GetSchemeAsync(string name)
    {
        if (name == AppAuthenticationSchemes.Bearer)
        {
            return base.GetSchemeAsync("TestAuth");
        }

        return base.GetSchemeAsync(name);
    }
}