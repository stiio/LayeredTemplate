using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Web.Mocks.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Web.Api.IntegrationTests.TestAuth;

public class TestAuthHandler : AuthenticationHandler<TestAuthAuthenticationOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<TestAuthAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = this.Context.Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Token not provided"));
        }

        if (!token.StartsWith(TestAuthAuthenticationOptions.DefaultScheme))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid scheme"));
        }

        token = token.Replace($"{TestAuthAuthenticationOptions.DefaultScheme} ", string.Empty);

        var mockUser = JsonSerializer.Deserialize<MockUserSettings>(token!)!;

        var claims = new[]
        {
            new Claim(TokenKeys.UserId, mockUser.Id!),
            new Claim(TokenKeys.Email, mockUser.Email!),
            new Claim(TokenKeys.Role, mockUser.Role ?? string.Empty),
            new Claim(TokenKeys.Phone, mockUser.Phone ?? string.Empty),
        };

        var identity = new ClaimsIdentity(claims, this.Options.AuthenticationType);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, this.Options.Scheme);

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}