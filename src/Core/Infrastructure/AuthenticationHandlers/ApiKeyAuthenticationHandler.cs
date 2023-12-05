using System.Security.Claims;
using System.Text.Encodings.Web;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Infrastructure.Extensions;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Shared.Extensions;
using LayeredTemplate.Shared.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace LayeredTemplate.Infrastructure.AuthenticationHandlers;

internal class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly IApplicationDbContext dbContext;
    private readonly AppSettings appSettings;

    public ApiKeyAuthenticationHandler(
        IApplicationDbContext dbContext,
        IOptions<AppSettings> appSettings,
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        this.appSettings = appSettings.Value;
        this.dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!this.appSettings.ApiKeysEnabled)
        {
            return AuthenticateResult.NoResult();
        }

        if (this.Request.Headers.ContainsKey(HeaderNames.Authorization))
        {
            return AuthenticateResult.NoResult();
        }

        if (!this.Request.Headers.ContainsKey(ApiKeyHeaderName))
        {
            return AuthenticateResult.Fail($"Header {ApiKeyHeaderName} not provided");
        }

        var secret = this.Request.Headers[ApiKeyHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(secret))
        {
            return AuthenticateResult.Fail("Api key not provided");
        }

        try
        {
            return await this.ValidateApiKey(secret);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Validate api key exception.");
            return AuthenticateResult.Fail("Validate api key exception");
        }
    }

    private async Task<AuthenticateResult> ValidateApiKey(string secret)
    {
        var apiKey = await this.dbContext.ApiKeys
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Secret == secret);

        if (apiKey is null)
        {
            return AuthenticateResult.Fail("Invalid api key");
        }

        var user = apiKey.User!;

        var claims = new List<Claim>();
        claims.AddIfNotNull(user.Id.ToString().CreateClaimIfNotNull(AppClaims.UserId));
        claims.AddIfNotNull(user.Email.CreateClaimIfNotNull(AppClaims.Email));
        claims.AddIfNotNull(user.Phone.CreateClaimIfNotNull(AppClaims.Phone));
        claims.AddIfNotNull(user.Role.ToString().CreateClaimIfNotNull(AppClaims.Role));
        claims.AddIfNotNull(user.EmailVerified.ToString().ToLower().CreateClaimIfNotNull(AppClaims.EmailVerified));
        claims.AddIfNotNull(user.PhoneVerified.ToString().ToLower().CreateClaimIfNotNull(AppClaims.PhoneVerified));
        claims.AddIfNotNull($"{user.FirstName} {user.LastName}".Trim().CreateClaimIfNotNull(AppClaims.Name));
        claims.AddIfNotNull(user.FirstName.CreateClaimIfNotNull(AppClaims.FirstName));
        claims.AddIfNotNull(user.LastName.CreateClaimIfNotNull(AppClaims.LastName));

        var identity = new ClaimsIdentity(claims, AppAuthenticationSchemes.ApiKey);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, this.Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}