using System.Text.Json;
using LayeredTemplate.Auth.ApiClient;
using LayeredTemplate.Auth.ApiClient.Clients;
using LayeredTemplate.Auth.ApiClient.Models;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthApiClient(builder.Configuration, opts =>
{
    opts.ClientId = "backend";
    opts.ClientSecret = "+qZW6uP+RmhkCZg9cwz2pF61AxAsQsOtfQNW6KG7YTw=";
    opts.BaseUrl = "https://localhost:8080";
    opts.Scopes = ["admin.users"];
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

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
});

app.Run();
