using LayeredTemplate.Auth.ApiClient.Models;

namespace LayeredTemplate.Auth.ApiClient.Clients;

/// <summary>
/// Client for Auth.Web's admin-users API (<c>/api/admin/users/*</c>).
/// Requires the configured OIDC client to hold the <c>admin.users</c> scope.
/// </summary>
public interface IAuthUsersClient
{
    /// <summary>Returns the user or <c>null</c> if not found.</summary>
    Task<UserResponse?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Returns the user or <c>null</c> if not found.</summary>
    Task<UserResponse?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Creates a user. Throws <see cref="AuthApiException"/> with 409 if the email is already in use.</summary>
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Applies a partial update. Null fields are left unchanged.</summary>
    Task<UserResponse> UpdateAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes the user. Silently succeeds if the user has already been deleted.</summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a single-use invite token (TTL is configured server-side, 30 days by default).
    /// Use the returned token to compose an accept-invite link:
    /// <c>{Auth.BaseUrl}/account/accept_invite?userId={UserId}&amp;code={Token}&amp;returnUrl=...</c>.
    /// </summary>
    Task<InviteTokenResponse> CreateInviteTokenAsync(string id, CancellationToken cancellationToken = default);
}
