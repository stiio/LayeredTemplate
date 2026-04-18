namespace LayeredTemplate.Auth.ApiClient.Models;

/// <summary>
/// One-time token for inviting a user to set their password and sign in.
/// Compose the link as <c>{Auth.BaseUrl}/account/accept_invite?userId={UserId}&amp;code={Token}&amp;returnUrl=...</c>.
/// </summary>
public record InviteTokenResponse(
    string UserId,
    string Token,
    DateTimeOffset ExpiresAt);
