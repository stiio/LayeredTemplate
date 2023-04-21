using System.Security.Claims;
using System.Text.Encodings.Web;
using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Infrastructure.Mocks.Authentication;

internal class MockAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly MockUserSettings mockUser;

    public MockAuthHandler(
        IOptions<MockUserSettings> mockUserOptions,
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
        this.mockUser = mockUserOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!this.Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Unauthorized"));
        }

        var authorizationHeader = this.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.Fail("Unauthorized"));
        }

        var claims = new[]
        {
            new Claim(TokenKeys.UserId, this.mockUser.Id!),
            new Claim(TokenKeys.Email, this.mockUser.Email!),
            new Claim(TokenKeys.Role, this.mockUser.Role ?? string.Empty),
            new Claim(TokenKeys.Phone, this.mockUser.Phone ?? string.Empty),
        };

        var identity = new ClaimsIdentity(claims, AppAuthenticationTypes.OAuth);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AppAuthenticationSchemes.OAuth);

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}