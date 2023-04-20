using System.Security.Claims;
using System.Text.Encodings.Web;
using LayeredTemplate.Domain.Enums;
using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Infrastructure.AuthenticationHandlers;

internal class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!this.Request.Headers.ContainsKey(ApiKeyHeaderName))
        {
            return AuthenticateResult.Fail("Unauthorized");
        }

        var apiKey = this.Request.Headers[ApiKeyHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.Fail("Unauthorized");
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.Fail("Unauthorized");
        }

        try
        {
            return await this.ValidateApiKey(apiKey);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Validate api key exception.");
            return AuthenticateResult.Fail(e.Message);
        }
    }

    private Task<AuthenticateResult> ValidateApiKey(string apiKey)
    {
        // TODO: Add validate api key
        var claims = new List<Claim>
        {
            new Claim(TokenKeys.UserId, "D87F1AD8-7F86-4FDD-B721-F69B05226DB4"),
            new Claim(TokenKeys.Role, Role.Client.ToString()),
            new Claim(ClaimTypes.Role, Role.Client.ToString()),
        };

        var identity = new ClaimsIdentity(claims, AppAuthenticationTypes.ApiKey);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, this.Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}