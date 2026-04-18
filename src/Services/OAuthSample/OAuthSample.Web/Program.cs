using System.Text.Json;
using LayeredTemplate.Auth.ApiClient;
using LayeredTemplate.Auth.ApiClient.Clients;
using LayeredTemplate.Auth.ApiClient.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

const string AuthIssuer = "https://localhost:8080";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthApiClient(builder.Configuration, opts =>
{
    opts.ClientId = "backend";
    opts.ClientSecret = "+qZW6uP+RmhkCZg9cwz2pF61AxAsQsOtfQNW6KG7YTw=";
    opts.BaseUrl = AuthIssuer;
    opts.Scopes = ["auth/admin.users"];
});

// JwtBearer validates access_tokens issued by Auth.Web. The Authority URL is used to fetch OIDC
// discovery + JWKS on first use (cached), so no manual key distribution is required.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = AuthIssuer;

        // RFC 9068 — OpenIddict tags access_tokens with typ=at+jwt. Constraining ValidTypes
        // ensures an id_token (typ=JWT) accidentally sent in the Authorization header is rejected.
        options.TokenValidationParameters.ValidTypes = ["at+jwt"];

        // Audience validation is off in this demo because no resource URI is registered for
        // the scopes the SPA requests. In production, associate an API scope with a resource
        // (see SeedOidcScopesTask in Auth.Web) and set ValidAudience = "api://oauth-sample".
        options.TokenValidationParameters.ValidateAudience = false;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/users/invite", async (
    HttpContext context,
    IAuthUsersClient authClient,
    IOptions<AuthApiClientOptions> authOptions) =>
{
    var body = await context.Request.ReadFromJsonAsync<JsonElement>();
    var userEmail = body.GetProperty("email").GetString()!;

    var user = await authClient.GetByEmailAsync(userEmail)
               ?? await authClient.CreateAsync(new CreateUserRequest { Email = userEmail });

    var invite = await authClient.CreateInviteTokenAsync(user.Id);

    // Correlates this invite end-to-end — flows through Auth.Web's accept_invite → SPA redirect →
    // OIDC login → finally surfaces on invite-complete. Backend-generated so the SPA can't forge it;
    // later a real invite entity can replace the plain GUID.
    var inviteId = Guid.CreateVersion7().ToString();

    // returnUrl points back to this SPA — must be in Auth.Web's CorsSettings.AllowedOrigins
    // for AcceptInvite to follow it (otherwise falls back to /account/manage on Auth.Web).
    var req = context.Request;
    var selfOrigin = $"{req.Scheme}://{req.Host}";
    var returnUrl = $"{selfOrigin}/invite-complete.html?inviteId={inviteId}";

    var inviteUrl =
        $"{authOptions.Value.BaseUrl.TrimEnd('/')}/account/accept_invite" +
        $"?userId={Uri.EscapeDataString(invite.UserId)}" +
        $"&code={Uri.EscapeDataString(invite.Token)}" +
        $"&returnUrl={Uri.EscapeDataString(returnUrl)}";

    return Results.Json(new { inviteUrl, inviteId, expiresAt = invite.ExpiresAt });
}).RequireAuthorization();

app.Run();
