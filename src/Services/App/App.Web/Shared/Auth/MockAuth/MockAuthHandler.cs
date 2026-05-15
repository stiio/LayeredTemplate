using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.App.Shared.Auth.MockAuth;

/// <summary>
/// Development-only auth handler: trusts a fake principal configured via <see cref="MockUserSettings"/>.
/// Enabled by setting <c>USE_MOCK_AUTH=true</c> in the configuration. Never wire this up in production.
/// </summary>
internal sealed class MockAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly MockUserSettings mockUser;

    public MockAuthHandler(
        IOptions<MockUserSettings> mockUserOptions,
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        this.mockUser = mockUserOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!this.Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var authorizationHeader = this.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.Fail("Unauthorized"));
        }

        var claims = new[]
        {
            new Claim(AppClaims.UserId, this.mockUser.Id!),
            new Claim(AppClaims.Email, this.mockUser.Email!),
            new Claim(AppClaims.EmailVerified, "true"),
            new Claim(AppClaims.Phone, this.mockUser.Phone ?? string.Empty),
        };

        var identity = new ClaimsIdentity(claims, AppAuthenticationSchemes.Bearer);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AppAuthenticationSchemes.Bearer);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
